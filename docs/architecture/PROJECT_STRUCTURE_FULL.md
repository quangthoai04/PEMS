# PEMS Project Structure (Full Tree)

- File này phản ánh cấu trúc thư mục **thật hiện tại** của project PEMS.
- Được cập nhật sau khi quét lại source code (lần quét: cấu trúc thật trên nhánh `Canh-Iter1`, ngày 2026-06-26).
- Không bao gồm các thư mục build/generated như `node_modules`, `dist`, `bin`, `obj`, `.vs`, `.git`, `.tmp-build`... Những thư mục này được đánh dấu `[excluded]` tại vị trí xuất hiện và **không** mở rộng nội dung bên trong.

## 1. Scope

Tài liệu này bao gồm:

- **Backend Clean Architecture** — `backend/PEMS.Api`, `backend/PEMS.Application`, `backend/PEMS.Domain`, `backend/PEMS.Infrastructure`, kèm 2 project tiện ích nhỏ `backend/CheckDb` và `backend/JsonTest`.
- **Frontend React** — `frontend/pems-react` (Vite + React + TypeScript, kiến trúc feature-based: `src/features/`, `src/pages/`, `src/components/`, `src/shared/`).
- **Database scripts** — `database/scripts` (hiện chỉ còn output build của `DbSeeder`); các file SQL fresh-create v10 + seeder source thực tế nằm trong `docs/database/scripts/`. Dự án dùng SQL fresh-create, **không có** thư mục EF migrations.
- **Documentation** — `docs/` (kiến trúc, use cases, permissions, database, authentication, và nhiều thư mục prompt/spec theo module).
- **Tests** — `tests/` (PEMS.ApplicationTests, PEMS.ArchitectureTests, PEMS.IntegrationTests, PEMS.UnitTests + http test files).
- **Root configuration files** — `.gitignore`, `.gitattributes`, `PEMS.slnx`, `README.md`, và một số file scratch/debug ở root.

## 2. Directory Tree

```text
PEMS/
├── .claude/
│   ├── settings.json
│   └── settings.local.json
├── .git/   [excluded]
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
│   │   ├── bin/   [excluded]
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
│   │   │   ├── EmailsController.cs
│   │   │   ├── FaqsController.cs
│   │   │   ├── FeedbacksController.cs
│   │   │   ├── GalleriesController.cs
│   │   │   ├── MeetingMinutesController.cs
│   │   │   ├── NewsController.cs
│   │   │   ├── PartnersController.cs
│   │   │   ├── ProfilesController.cs
│   │   │   ├── PublicContentController.cs
│   │   │   ├── PublicPartnersController.cs
│   │   │   ├── ReportsController.cs
│   │   │   ├── VisitInvitationsController.cs
│   │   │   └── VisitRequestsController.cs
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
│   │   ├── obj/   [excluded]
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json
│   │   ├── Pems_WebAPI.http
│   │   ├── PEMS.Api.csproj
│   │   └── Program.cs
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
│   │   ├── bin/   [excluded]
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
│   │   │   ├── Interfaces/
│   │   │   │   ├── IApplicationDbContext.cs
│   │   │   │   ├── IApprovalRoutingService.cs
│   │   │   │   ├── IAuditLogService.cs
│   │   │   │   ├── ICampusRepository.cs
│   │   │   │   ├── ICurrentUserService.cs
│   │   │   │   ├── IDateTimeService.cs
│   │   │   │   ├── IDelegationRepository.cs
│   │   │   │   ├── IDocumentRepository.cs
│   │   │   │   ├── IEmailService.cs
│   │   │   │   ├── IExternalApiClient.cs
│   │   │   │   ├── IFaceRecognitionService.cs
│   │   │   │   ├── IFeidIdentityVerifier.cs
│   │   │   │   ├── IFileStorageService.cs
│   │   │   │   ├── IFileValidationService.cs
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
│   │   │   │   ├── PagedResult.cs
│   │   │   │   ├── PaginatedResult.cs
│   │   │   │   ├── PaginationRequest.cs
│   │   │   │   ├── Result.cs
│   │   │   │   └── ResultOfT.cs
│   │   │   └── Security/
│   │   │       ├── AuthErrorCodes.cs
│   │   │       ├── AuthOptions.cs
│   │   │       ├── EffectiveRole.cs
│   │   │       ├── IHtmlSanitizerService.cs
│   │   │       ├── IRoleAccessPolicy.cs
│   │   │       ├── PasswordPolicy.cs
│   │   │       ├── PemsClaimTypes.cs
│   │   │       ├── RoleAccessPolicy.cs
│   │   │       ├── RoleCode.cs
│   │   │       ├── SubRole.cs
│   │   │       └── UseCasePermissionAttribute.cs
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
│   │   │   │   ├── ScanBusinessCard/
│   │   │   │   │   ├── ScanBusinessCardCommand.cs
│   │   │   │   │   ├── ScanBusinessCardCommandHandler.cs
│   │   │   │   │   ├── ScanBusinessCardCommandValidator.cs
│   │   │   │   │   └── ScanBusinessCardResponse.cs
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
│   │   │   │   ├── GetHostCandidates/
│   │   │   │   │   ├── GetHostCandidatesQuery.cs
│   │   │   │   │   ├── GetHostCandidatesQueryHandler.cs
│   │   │   │   │   └── HostCandidateDto.cs
│   │   │   │   ├── GetSubmittedVisitRequestFormDetail/
│   │   │   │   │   ├── GetSubmittedVisitRequestFormDetailQuery.cs
│   │   │   │   │   ├── GetSubmittedVisitRequestFormDetailQueryHandler.cs
│   │   │   │   │   └── SubmittedVisitRequestFormDetailDto.cs
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
│   │   │   │   │   └── ViewGuestDelegationListQueryHandler.cs
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
│   │   │   │   └── RejectRequest/
│   │   │   │       └── RejectRequestCommand.cs
│   │   │   └── Queries/
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
│   │   │       ├── SearchandFilterDepartments/
│   │   │       │   ├── SearchandFilterDepartmentsQuery.cs
│   │   │       │   └── SearchandFilterDepartmentsQueryHandler.cs
│   │   │       ├── SearchCoordinationTasks/
│   │   │       │   ├── SearchCoordinationTasksDto.cs
│   │   │       │   ├── SearchCoordinationTasksQuery.cs
│   │   │       │   └── SearchCoordinationTasksQueryHandler.cs
│   │   │       ├── SearchPersonnel/
│   │   │       │   ├── SearchPersonnelDto.cs
│   │   │       │   ├── SearchPersonnelQuery.cs
│   │   │       │   └── SearchPersonnelQueryHandler.cs
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
│   │   │       └── ViewDocumentList/
│   │   │           ├── ViewDocumentListDto.cs
│   │   │           ├── ViewDocumentListQuery.cs
│   │   │           └── ViewDocumentListQueryHandler.cs
│   │   ├── Emails/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateEmailTemplate/
│   │   │   │   │   ├── CreateEmailTemplateCommand.cs
│   │   │   │   │   ├── CreateEmailTemplateCommandHandler.cs
│   │   │   │   │   ├── CreateEmailTemplateCommandValidator.cs
│   │   │   │   │   └── CreateEmailTemplateResponse.cs
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
│   │   │   │   └── UpdateEmailTemplate/
│   │   │   │       ├── UpdateEmailTemplateCommand.cs
│   │   │   │       ├── UpdateEmailTemplateCommandHandler.cs
│   │   │   │       ├── UpdateEmailTemplateCommandValidator.cs
│   │   │   │       └── UpdateEmailTemplateResponse.cs
│   │   │   └── Queries/
│   │   │       ├── GetUnprocessedEmailCount/
│   │   │       │   ├── GetUnprocessedEmailCountQuery.cs
│   │   │       │   └── GetUnprocessedEmailCountQueryHandler.cs
│   │   │       ├── ViewEmail/
│   │   │       │   ├── ViewEmailDto.cs
│   │   │       │   ├── ViewEmailQuery.cs
│   │   │       │   └── ViewEmailQueryHandler.cs
│   │   │       ├── ViewEmailList/
│   │   │       │   ├── ViewEmailListDto.cs
│   │   │       │   ├── ViewEmailListQuery.cs
│   │   │       │   └── ViewEmailListQueryHandler.cs
│   │   │       ├── ViewEmailTemplateDetail/
│   │   │       │   ├── ViewEmailTemplateDetailDto.cs
│   │   │       │   ├── ViewEmailTemplateDetailQuery.cs
│   │   │       │   └── ViewEmailTemplateDetailQueryHandler.cs
│   │   │       └── ViewEmailTemplateList/
│   │   │           ├── ViewEmailTemplateListDto.cs
│   │   │           ├── ViewEmailTemplateListQuery.cs
│   │   │           └── ViewEmailTemplateListQueryHandler.cs
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
│   │   ├── Galleries/
│   │   │   ├── Commands/
│   │   │   │   ├── AddGalleryItem/
│   │   │   │   │   ├── AddGalleryItemCommand.cs
│   │   │   │   │   ├── AddGalleryItemCommandHandler.cs
│   │   │   │   │   ├── AddGalleryItemCommandValidator.cs
│   │   │   │   │   └── AddGalleryItemResponse.cs
│   │   │   │   ├── DeleteGalleryItem/
│   │   │   │   │   ├── DeleteGalleryItemCommand.cs
│   │   │   │   │   ├── DeleteGalleryItemCommandHandler.cs
│   │   │   │   │   ├── DeleteGalleryItemCommandValidator.cs
│   │   │   │   │   └── DeleteGalleryItemResponse.cs
│   │   │   │   └── UpdateGalleryItem/
│   │   │   │       ├── UpdateGalleryItemCommand.cs
│   │   │   │       ├── UpdateGalleryItemCommandHandler.cs
│   │   │   │       ├── UpdateGalleryItemCommandValidator.cs
│   │   │   │       └── UpdateGalleryItemResponse.cs
│   │   │   └── Queries/
│   │   │       ├── SearchGalleryItems/
│   │   │       │   ├── SearchGalleryItemsDto.cs
│   │   │       │   ├── SearchGalleryItemsQuery.cs
│   │   │       │   └── SearchGalleryItemsQueryHandler.cs
│   │   │       └── ViewGalleryItemList/
│   │   │           ├── ViewGalleryItemListDto.cs
│   │   │           ├── ViewGalleryItemListQuery.cs
│   │   │           └── ViewGalleryItemListQueryHandler.cs
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
│   │   │   │   └── PublishNews/
│   │   │   │       ├── PublishNewsCommand.cs
│   │   │   │       ├── PublishNewsCommandHandler.cs
│   │   │   │       ├── PublishNewsCommandValidator.cs
│   │   │   │       └── PublishNewsResponse.cs
│   │   │   └── Queries/
│   │   │       ├── ViewNewsDetails/
│   │   │       │   ├── ViewNewsDetailsDto.cs
│   │   │       │   ├── ViewNewsDetailsQuery.cs
│   │   │       │   └── ViewNewsDetailsQueryHandler.cs
│   │   │       └── ViewNewsList/
│   │   │           ├── ViewNewsListDto.cs
│   │   │           ├── ViewNewsListQuery.cs
│   │   │           ├── ViewNewsListQueryHandler.cs
│   │   │           └── ViewNewsListQueryValidator.cs
│   │   ├── obj/   [excluded]
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
│   │   │   │   └── UpdateProfile/
│   │   │   │       ├── UpdateProfileCommand.cs
│   │   │   │       ├── UpdateProfileCommandHandler.cs
│   │   │   │       └── UpdateProfileCommandValidator.cs
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
│   │   │   │   └── ViewPolicyAndTerms/
│   │   │   │       ├── ViewPolicyAndTermsDto.cs
│   │   │   │       ├── ViewPolicyAndTermsQuery.cs
│   │   │   │       └── ViewPolicyAndTermsQueryHandler.cs
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
│   │   ├── DependencyInjection.cs
│   │   └── PEMS.Application.csproj
│   ├── PEMS.Domain/
│   │   ├── .tmp-build/   [excluded]
│   │   ├── bin/   [excluded]
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
│   │   │   │   ├── EmailTemplate.cs
│   │   │   │   ├── SentEmail.cs
│   │   │   │   └── SentEmailRecipient.cs
│   │   │   ├── Faqs/
│   │   │   │   └── Faq.cs
│   │   │   ├── Feedbacks/
│   │   │   │   ├── Feedback.cs
│   │   │   │   └── FeedbackRatingItem.cs
│   │   │   ├── Galleries/
│   │   │   │   ├── Gallery.cs
│   │   │   │   ├── GalleryImage.cs
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
│   │   │   ├── VisitRequestStatus.cs
│   │   │   └── VisitScope.cs
│   │   ├── Events/
│   │   │   ├── AccountCreatedEvent.cs
│   │   │   ├── DelegationClosedEvent.cs
│   │   │   ├── NewsApprovedEvent.cs
│   │   │   ├── ResourceRequestApprovedEvent.cs
│   │   │   ├── VisitRequestApprovedEvent.cs
│   │   │   └── VisitRequestSubmittedEvent.cs
│   │   ├── obj/   [excluded]
│   │   ├── ValueObjects/
│   │   │   ├── Address.cs
│   │   │   ├── DateRange.cs
│   │   │   ├── EmailAddress.cs
│   │   │   ├── FileMetadata.cs
│   │   │   └── PhoneNumber.cs
│   │   └── PEMS.Domain.csproj
│   └── PEMS.Infrastructure/
│       ├── .tmp-build/   [excluded]
│       ├── bin/   [excluded]
│       ├── Common/
│       │   └── DateTimeService.cs
│       ├── Email/
│       │   ├── EmailService.cs
│       │   ├── EmailTemplateRenderer.cs
│       │   └── SmtpEmailSender.cs
│       ├── ExternalServices/
│       │   ├── ApiClient/
│       │   │   └── ExternalApiClient.cs
│       │   ├── Calendar/
│       │   │   └── CalendarIntegrationService.cs
│       │   ├── FaceRecognition/
│       │   │   └── FaceRecognitionService.cs
│       │   └── Ocr/
│       │       └── OcrService.cs
│       ├── FileStorage/
│       │   ├── CloudFileStorageService.cs
│       │   ├── FileStorageService.cs
│       │   ├── FileValidationService.cs
│       │   ├── LocalFileStorageService.cs
│       │   └── VirusScanService.cs
│       ├── Idempotency/
│       │   └── IdempotencyService.cs
│       ├── Identity/
│       │   ├── CurrentUserService.cs
│       │   ├── FeidIdentityVerifier.cs
│       │   ├── GoogleTokenValidator.cs
│       │   ├── JwtTokenService.cs
│       │   ├── NotificationService.cs
│       │   ├── OtpService.cs
│       │   ├── OwnershipChecker.cs
│       │   ├── PasswordHasher.cs
│       │   ├── RefreshTokenStore.cs
│       │   ├── SecureTokenGenerator.cs
│       │   └── SessionService.cs
│       ├── Logging/
│       │   ├── ApiRequestLogService.cs
│       │   ├── AuditLogService.cs
│       │   └── SecurityAuditService.cs
│       ├── obj/   [excluded]
│       ├── Persistence/
│       │   ├── Configurations/
│       │   │   └── UserConfiguration.cs
│       │   ├── Repositories/
│       │   │   ├── CampusRepository.cs
│       │   │   ├── DelegationRepository.cs
│       │   │   ├── DocumentRepository.cs
│       │   │   ├── GenericRepository.cs
│       │   │   ├── PartnerRepository.cs
│       │   │   ├── ReportRepository.cs
│       │   │   └── UserRepository.cs
│       │   ├── ApplicationDbContext.cs
│       │   └── ApplicationDbContextFactory.cs
│       ├── RateLimiting/
│       │   ├── InMemoryRateLimitStore.cs
│       │   ├── RateLimitService.cs
│       │   └── RedisRateLimitStore.cs
│       ├── Security/
│       │   └── HtmlSanitizerService.cs
│       ├── Services/
│       │   ├── ApprovalRoutingService.cs
│       │   ├── UserProvisionService.cs
│       │   └── VisitRequestService.cs
│       ├── DependencyInjection.cs
│       └── PEMS.Infrastructure.csproj
├── database/
│   └── scripts/
│       └── DbSeeder/
│           ├── bin/   [excluded]
│           └── obj/   [excluded]
├── docs/
│   ├── account-management/
│   │   ├── PROMPT_UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER_PEMS.md
│   │   ├── UC_StaffLeader_Related_Visitor_Accounts_Tab.md
│   │   └── UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER.md
│   ├── architecture/
│   │   ├── PROJECT_STRUCTURE_FULL.md
│   │   └── REFACTOR_CHANGELOG.md
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
│   ├── CampusManagement/
│   │   ├── 00_CAMPUS_MANAGEMENT_COMMON_RULES_HO.md
│   │   ├── 01_UC82_VIEW_CAMPUS_LIST_HO.md
│   │   ├── 02_UC83_SEARCH_FILTER_CAMPUS_HO.md
│   │   ├── 03_UC81_CREATE_CAMPUS_HO.md
│   │   ├── 04_UC84_VIEW_CAMPUS_DETAILS_HO.md
│   │   ├── 05_UC85_UPDATE_CAMPUS_HO.md
│   │   └── 06_UC86_MANAGE_CAMPUS_STATUS_HO.md
│   ├── database/
│   │   ├── scripts/
│   │   │   ├── DbSeeder/
│   │   │   │   ├── DbSeeder.csproj
│   │   │   │   └── Program.cs
│   │   │   ├── cleanup_expired_user_sessions.sql
│   │   │   └── pems_full_v10_new.sql
│   │   ├── Table/
│   │   │   ├── cleanup_expired_user_sessions.sql
│   │   │   └── PEMS_v8_4_refined_v6_v10_FULL_SQL_TABLE_FIELD_DICTIONARY.docx
│   │   ├── DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
│   │   └── PROMPT_UPDATE_CODE_FOR_SQL_V10_PEMS.md
│   ├── delegation/
│   │   ├── setup delegation/
│   │   │   └── PEMS_VISIT_DETAIL_PROCESS_LOGIC_REQUIREMENTS.md
│   │   ├── UC17_submitform/
│   │   │   ├── PROMPT_AUDIT_SYNC_UC17_WITH_SQL_FULL.md
│   │   │   ├── PROMPT_FIX_UC17_CONTACT_EMAIL_NON_VISITOR_CONFLICT.md
│   │   │   ├── PROMPT_FIX_UC17_CONTACT_PERSON_ACCOUNT_SCOPE_AND_TSC_FINAL.md
│   │   │   ├── PROMPT_FIX_UC17_PUBLIC_FORM_UI_AND_SQL_ALIGNMENT (1).md
│   │   │   ├── uc17 submit form.md
│   │   │   └── UC17_SUBMIT_VISIT_REQUEST_SYNC_REPORT.md
│   │   ├── view form to approve/
│   │   │   ├── PEMS_MULTI_CAMPUS_EXPANDABLE_ROW_OPTION_A.md
│   │   │   ├── PROMPT_IMPLEMENT_APPROVE_REJECT_CANCEL_REASON_VISIBILITY_PEMS.md
│   │   │   └── PROMPT_IMPLEMENT_PRE_APPROVAL_VISIT_REQUEST_REVIEW_PEMS.md
│   │   ├── view list visiting/
│   │   │   ├── PROMPT_FIX_HO_SINGLE_CAMPUS_VISIBILITY_CODE.md
│   │   │   ├── PROMPT_FIX_VISIT_MANAGEMENT_HOST_STATUS_SEARCH_SORT_SQL_ALIGNMENT.md
│   │   │   ├── PROMPT_FIX_VISIT_ROLE_UI_FILTERS_AND_SEED_LOGIC.md
│   │   │   ├── PROMPT_IMPLEMENT_VISIT_REQUEST_ROLE_TABS_PEMS.md
│   │   │   ├── PROMPT_UPDATE_ROLE_BASED_VISIT_FILTERS_PEMS.md
│   │   │   ├── PROMPT_UPDATE_VISIT_PARTICIPANTS_4_ROLES.md
│   │   │   └── PROMPT_UPDATE_VISIT_REQUEST_ROLE_BASED_LOGIC.md
│   │   └── PEMS_DELEGATION_VISIT_MANAGEMENT_UPDATE_REQUIREMENTS.md
│   ├── Department/
│   │   ├── PEMS_DEPARTMENT_PERSONNEL_SHORT_FUNCTION_PROMPT.md
│   │   ├── PEMS_DEPT_LEADER_LOGISTICS_ASSIGNMENT_FLOW_PROMPT.md
│   │   ├── PEMS_DEPT_RECEPTION_TASKS_REAL_DATA_PROMPT.md
│   │   ├── PEMS_DEPTLEAD_DASHBOARD_EMAIL_LOCAL_DRAFT_PROMPT.md
│   │   ├── PEMS_DEPTLEAD_UI_RESTORE_ACTIONS_PROMPT - Copy.md
│   │   ├── PEMS_DEPTLEAD_UI_RESTORE_ACTIONS_PROMPT.md
│   │   └── PEMS_EMAIL_MANAGEMENT_REAL_DATA_WORKFLOW_PROMPT.md
│   ├── Department_Staff_Leader/
│   │   ├── UC-101_ADD_NEW_DEPARTMENT_STAFF_LEADER.md
│   │   ├── UC-102_UPDATE_DEPARTMENT_STAFF_LEADER.md
│   │   ├── UC-103_SEARCH_FILTER_DEPARTMENTS_STAFF_LEADER.md
│   │   ├── UC-104_VIEW_DEPARTMENT_LIST_STAFF_LEADER.md
│   │   ├── UC-105_VIEW_DEPARTMENT_DETAILS_STAFF_LEADER.md
│   │   └── UC-106_MANAGE_DEPARTMENT_STATUS_STAFF_LEADER.md
│   ├── GUIDE CLAUDE/
│   │   ├── architecture/
│   │   │   └── CLEAN_ARCHITECTURE.md
│   │   ├── FRONTEND/
│   │   │   └── PEMS_UI_DESIGN_SYSTEM_PROMPT.md
│   │   └── PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── permissions/
│   │   ├── PERMISSION_MATRIX.md
│   │   └── PERMISSION_RULES.md
│   ├── ProfileManagement/
│   │   ├── 00_README_PROFILE_UC_IMPLEMENTATION.md
│   │   ├── 01_UC14_VIEW_PROFILE_SPEC.md
│   │   ├── 02_UC15_UPDATE_PROFILE_TEXT_SPEC.md
│   │   ├── 06_BACKEND_IMPLEMENTATION_CHECKLIST.md
│   │   ├── 07_FRONTEND_IMPLEMENTATION_CHECKLIST.md
│   │   └── 08_TEST_CASES_AND_ACCEPTANCE_CRITERIA.md
│   ├── Prompt/
│   │   ├── PROMPT_CODE_UC05_VIEW_FAQ_BACKEND_UPDATED_PROJECT_STRUCTURE.md
│   │   ├── PROMPT_CODE_UC62_VIEW_LIST_FAQ_BACKEND.md
│   │   ├── PROMPT_CODE_UC63_CREATE_FAQ_BACKEND.md
│   │   ├── PROMPT_CODE_UC64_UPDATE_FAQ_BACKEND.md
│   │   └── PROMPT_CODE_UC88_VIEW_NEWS_LIST_BACKEND.md
│   ├── todo/
│   │   └── PEMS_AUTH_NEWS_SECURITY_TODO.md
│   ├── use-cases/
│   │   ├── USE_CASE_LIST.md
│   │   └── USE_CASE_NOTES.md
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
│       │   ├── applet_update_contact.js
│       │   ├── applet_update_emerald.js
│       │   ├── applet_update_visit_3.js
│       │   ├── applet_update_visit_4.js
│       │   ├── applet_update_vp.js
│       │   ├── applet_update.js
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
│       │   │   │   ├── hola_new.jpg
│       │   │   │   ├── Hola.jpg
│       │   │   │   ├── QuanAP.jpg
│       │   │   │   └── QuyNhon.png
│       │   │   ├── images/
│       │   │   │   ├── 2021-FPTU-Eng.png
│       │   │   │   ├── banner_partner.png
│       │   │   │   ├── banner.jpg
│       │   │   │   ├── banner02.png
│       │   │   │   ├── loading.png
│       │   │   │   ├── news_pattern.svg
│       │   │   │   └── regenerated_image_1778552336496.png
│       │   │   ├── img_visit_detail/
│       │   │   │   ├── 01.jpg
│       │   │   │   ├── 02.jpg
│       │   │   │   ├── 03.jpg
│       │   │   │   ├── 04.jpg
│       │   │   │   ├── 05.jpg
│       │   │   │   ├── 06.jpg
│       │   │   │   ├── 07.jpg
│       │   │   │   ├── 08.jpg
│       │   │   │   ├── 09.jpg
│       │   │   │   ├── 10.jpg
│       │   │   │   ├── 11.jpg
│       │   │   │   ├── 12.jpg
│       │   │   │   ├── 13.jpg
│       │   │   │   ├── 14.jpg
│       │   │   │   ├── 15.jpg
│       │   │   │   ├── 16.jpg
│       │   │   │   ├── 17.jpg
│       │   │   │   ├── 18.jpg
│       │   │   │   ├── 19.jpg
│       │   │   │   └── 20.jpg
│       │   │   └── Logo/
│       │   │       ├── logo01.png
│       │   │       ├── logo02.png
│       │   │       ├── logo03.png
│       │   │       ├── logo04.png
│       │   │       ├── logo05.png
│       │   │       ├── logo06.png
│       │   │       ├── logo07.png
│       │   │       ├── logo08.png
│       │   │       ├── logo09.png
│       │   │       ├── logo10.png
│       │   │       ├── logo11.png
│       │   │       ├── logo12.png
│       │   │       ├── logo13.jpg
│       │   │       ├── logo14.png
│       │   │       ├── logo15.png
│       │   │       ├── logo16.png
│       │   │       ├── logo17.png
│       │   │       └── logo18.png
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
│       │   │   │   ├── Footer.tsx
│       │   │   │   └── Header.tsx
│       │   │   ├── modals/
│       │   │   │   ├── AssignHostModal.tsx
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
│       │   │   │   │   ├── authenticationApi.ts
│       │   │   │   │   └── authError.ts
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
│       │   │   │   │   ├── RejectedReasonModal.tsx
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
│       │   │   │   │   └── useDocuments.ts
│       │   │   │   └── types/
│       │   │   │       └── documents.types.ts
│       │   │   ├── emails/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── emailsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── emailsApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   ├── useEmails.ts
│       │   │   │   │   └── useLocalEmailDraft.ts
│       │   │   │   └── types/
│       │   │   │       └── emails.types.ts
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
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useNotifications.ts
│       │   │   │   └── types/
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
│       │   │   │   ├── departments/
│       │   │   │   │   ├── DepartmentDetailDashboard.tsx
│       │   │   │   │   ├── DepartmentManagement.tsx
│       │   │   │   │   ├── SharedDashboardView.tsx
│       │   │   │   │   ├── TaskDetail.tsx
│       │   │   │   │   └── TaskInvitationDetail.tsx
│       │   │   │   ├── documents/
│       │   │   │   │   └── DocumentManagement.tsx
│       │   │   │   ├── emails/
│       │   │   │   │   ├── CreateEmail.tsx
│       │   │   │   │   ├── EditEmail.tsx
│       │   │   │   │   ├── EmailDetail.tsx
│       │   │   │   │   ├── EmailManagement.tsx
│       │   │   │   │   ├── SendEmailTab.tsx
│       │   │   │   │   └── SentEmailDetail.tsx
│       │   │   │   ├── faq/
│       │   │   │   │   ├── FAQDetail.tsx
│       │   │   │   │   └── FAQManagement.tsx
│       │   │   │   ├── feedback/
│       │   │   │   │   ├── FeedbackDetail.tsx
│       │   │   │   │   ├── FeedbackManagement.tsx
│       │   │   │   │   └── mockData.ts
│       │   │   │   ├── gallery/
│       │   │   │   │   ├── GalleryManagement.tsx
│       │   │   │   │   └── LocationManagement.tsx
│       │   │   │   ├── home/
│       │   │   │   │   ├── AdminDashboardView.tsx
│       │   │   │   │   ├── DashboardHome.tsx
│       │   │   │   │   ├── DeptLeadDashboardView.tsx
│       │   │   │   │   ├── HODashboardView.tsx
│       │   │   │   │   └── SharedDashboardView.tsx
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
│       │   │   │   │   ├── mockReportData.ts
│       │   │   │   │   └── ReportManagement.tsx
│       │   │   │   └── visit/
│       │   │   │       ├── AgendaTemplateManagement.tsx
│       │   │   │       ├── CreateVisitRequest.tsx
│       │   │   │       ├── DeptLeadAssignmentTab.tsx
│       │   │   │       ├── DeptLeadVisitTasksPage.tsx
│       │   │   │       ├── HoVisitProcessDetail.tsx
│       │   │   │       ├── MinutesCard.tsx
│       │   │   │       ├── VisitAfterTab.tsx
│       │   │   │       ├── VisitDuringTab.tsx
│       │   │   │       ├── VisitNewsSection.tsx
│       │   │   │       ├── VisitParticipantInvitationDetail.tsx
│       │   │   │       ├── VisitProcess.tsx
│       │   │   │       ├── VisitRequestDetail.tsx
│       │   │   │       └── VisitRequestManagement.tsx
│       │   │   ├── CampusDetailVisitPage.tsx
│       │   │   ├── FAQPage.tsx
│       │   │   ├── ForbiddenPage.tsx
│       │   │   ├── HomePage.tsx
│       │   │   ├── InvalidAccountPage.tsx
│       │   │   ├── NewsDetailPage.tsx
│       │   │   ├── NewsPage.tsx
│       │   │   ├── PartnerDetailPage.tsx
│       │   │   ├── PartnersPage.tsx
│       │   │   └── VisitFPTUPage.tsx
│       │   ├── shared/
│       │   │   ├── api/
│       │   │   │   ├── authInterceptor.ts
│       │   │   │   ├── endpoints.ts
│       │   │   │   ├── errorHandler.ts
│       │   │   │   └── httpClient.ts
│       │   │   ├── auth/
│       │   │   │   ├── AuthContext.tsx
│       │   │   │   ├── authStorage.ts
│       │   │   │   ├── dashboardRoute.ts
│       │   │   │   ├── permissionChecker.ts
│       │   │   │   ├── ProtectedRoute.tsx
│       │   │   │   ├── resolveEffectiveRole.ts
│       │   │   │   └── RoleGuard.tsx
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
│       │   │       ├── formatUtils.ts
│       │   │       ├── passwordPolicy.ts
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
│       ├── fix_mojibake.cjs
│       ├── index.html
│       ├── metadata.json
│       ├── out.txt
│       ├── package-lock.json
│       ├── package.json
│       ├── README.md
│       ├── replace.cjs
│       ├── replace.js
│       ├── replace2.cjs
│       ├── tsconfig.json
│       └── vite.config.ts
├── scripts/
│   └── guard-project-structure.ps1
├── temp_hash/
│   ├── node_modules/   [excluded]
│   ├── package-lock.json
│   └── package.json
├── tests/
│   ├── http/
│   │   └── auth_dual_portal_manual_tests.http
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
│   │   │   ├── SearchandFilterDepartmentsQueryTests.cs
│   │   │   ├── SearchCoordinationTasksQueryTests.cs
│   │   │   ├── SearchPersonnelQueryTests.cs
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
│   └── temp_bcrypt/
│       ├── bin/   [excluded]
│       ├── obj/   [excluded]
│       ├── Program.cs
│       └── temp_bcrypt.csproj
├── .gitattributes
├── .gitignore
├── output.json
├── output1.txt
├── output2.txt
├── payload.json
├── PEMS.slnx
├── README.md
└── response.json
```

## 3. Layer Overview

Tóm tắt vai trò từng khu vực (chỉ ghi nhận ở mức structure):

- **`backend/PEMS.Api`** — API layer. Chứa `Controllers/` (23 controller, gồm cả `DepartmentReceptionTasksController`), `Middleware/`, `Filters/`, `Extensions/` (đăng ký service/auth/cors/rate-limit/swagger), `Contracts/` (ApiResponse, ApiRoutes), `Program.cs`, `appsettings*.json`.
- **`backend/PEMS.Application`** — use case layer theo CQRS. Mỗi module (Accounts, AgendaTemplates, ApiIntegrations, Authentication, Calendars, Campuses, Dashboard, Delegations, DepartmentReceptionTasks, Departments, Documents, Emails, Faqs, Feedbacks, Galleries, MeetingMinutes, News, Partners, Profiles, ...) chia thành `Commands/`, `Queries/`, và thường có `Common/`, `Mappings/`, `Dtos/`, `Rules/`. `Common/` cấp Application chứa `Behaviours/` (MediatR pipeline), `Exceptions/`, `Interfaces/` (port), `Models/`, `Security/`.
- **`backend/PEMS.Domain`** — domain layer. `Entities/` nhóm theo aggregate (AgendaTemplates, ApiIntegrations, Calendar, Campuses, Delegations, Departments, Documents, Emails, Faqs, Feedbacks, Galleries, Minutes, News, Notifications, Partners, Users), `Enums/`, `Events/`, `ValueObjects/`, `Constants/`, `Common/` (BaseEntity, AuditableEntity, SoftDeleteEntity, DomainEvent).
- **`backend/PEMS.Infrastructure`** — persistence + external services. `Persistence/` (ApplicationDbContext, Repositories, Configurations), `Identity/`, `Email/`, `FileStorage/`, `ExternalServices/` (ApiClient, Calendar, FaceRecognition, Ocr), `Logging/`, `RateLimiting/`, `Idempotency/`, `Security/`, `Services/`, `DependencyInjection.cs`.
- **`frontend/pems-react`** — React client. `src/features/<module>/` theo cấu trúc `api/` + `adapters/` + `hooks/` + `types/` (+ `components/`, `config/`, `schema/`, `utils/` khi cần); `src/pages/` (public pages + `dashboard/<area>/`); `src/components/` (layout, modals, dashboard, home, partners); `src/shared/` (api client, auth, constants, hooks, types, utils, security). **Không có** thư mục `routes/` hay `store/` riêng — routing nằm trong `App.tsx` + `src/shared/constants/appRoutes.ts`, state auth nằm trong `src/shared/auth/AuthContext.tsx`.
- **`database/`** — hiện chỉ chứa `scripts/DbSeeder/` (output build). Schema SQL + seeder source thực tế được đặt trong `docs/database/scripts/` (`pems_full_v10_new.sql`, `cleanup_expired_user_sessions.sql`, `DbSeeder/`).
- **`docs/`** — tài liệu kiến trúc, use case, permissions, API, database, authentication, và các thư mục prompt/spec theo module (account-management, CampusManagement, delegation, Department, Department_Staff_Leader, ProfileManagement, Prompt, ...).
- **`tests/`** — test solution: `PEMS.ApplicationTests` (unit test theo module CQRS), `PEMS.ArchitectureTests`, `PEMS.IntegrationTests`, `PEMS.UnitTests`, và `http/` (manual http tests).

## 4. Important Notes

Các điểm phát hiện khi quét (chỉ ghi nhận ở mức structure, không đánh giá logic):

**Module / folder mới so với tài liệu cũ**
- `backend/PEMS.Api/Controllers/DepartmentReceptionTasksController.cs` và module Application mới `backend/PEMS.Application/DepartmentReceptionTasks/` (Commands + Queries cho luồng tiếp nhận/điều phối của department).
- `Accounts/Queries/RelatedVisitors/` + `Accounts/Common/RelatedVisitorScope.cs` (tab "Related Visitor Accounts" của Staff Leader) và phía FE `features/account-management/components/RelatedVisitorsTab.tsx`, `hooks/useRelatedVisitors.ts`.
- `AgendaTemplates/` mở rộng nhiều: thêm command `ApplyAgendaTemplate`, `SetAgendaTemplateDefault`; thêm `Common/`; thêm query `GetAgendaSetupForInstance`, `GetDefaultAgendaTemplate`, `ViewAgendaTemplateDefaults`. Domain thêm entity `AgendaTemplateDefault.cs`.
- `Delegations/` mở rộng: thêm command `CompleteVisitStage`, `SaveVisitAgenda`, `UpdateRegistrantInfo`; thêm nhóm `Delegations/Minutes/` (lock/save minutes, participants, action items) và `Delegations/News/` (visit-instance news); thêm query `GetAgendaResponsibleCandidates`, `GetSubmittedVisitRequestFormDetail`, `GetVisitProcessDetail`, `GetVisitProcessPermissions`.
- Thêm `Common/` cho các module `Campuses`, `Departments`, `Profiles`; thêm query/command như `Campuses/.../GetCampusFilterOptions`, `Emails/.../MarkEmailCompleted`, `Emails/.../GetUnprocessedEmailCount`, `Emails/.../ViewEmailList`, `Faqs/.../ViewFAQDetail`.
- Domain `Constants/` bổ sung `FaqConstants.cs`, `NewsConstants.cs`, `VisitTypes.cs`; Domain `Entities/Delegations/VisitLogisticsAssignmentAttempt.cs` mới.
- Frontend thêm feature `department-reception-tasks/`; thêm `features/agenda-templates/components/AgendaSetupPanel.tsx`; thêm nhóm component reason cho `delegations/`; thêm `profile/components/NationalitySearchableDropdown.tsx` + `profile/constants/nationalities.ts`; thêm trang/section `MinutesCard.tsx`, `VisitNewsSection.tsx`, `HoVisitProcessDetail.tsx`, `SharedDashboardView.tsx`.
- Thư mục docs mới: `docs/CampusManagement/`, `docs/Department_Staff_Leader/`, `docs/ProfileManagement/`, `docs/Prompt/`, và các thư mục con `docs/delegation/setup delegation/`, `docs/delegation/view form to approve/`.
- Ở root xuất hiện `.vs/` (cache Visual Studio — đã `[excluded]`).

**Folder / file cũ không còn**
- `backend/test_api.cs` và `backend/test.cs` (file scratch ở backend root trong tài liệu cũ) đã không còn.
- Thư mục `database/` cũ từng chứa SQL fresh-create + `database/README.md`; hiện `database/scripts/DbSeeder/` chỉ còn `bin/`+`obj/`, không còn file `.sql` hay `README.md` ở đây. File SQL đã đổi tên/di chuyển: từ `pems_full_create_manual_..._FULL.sql` → `docs/database/scripts/pems_full_v10_new.sql`.
- Một số DTO bị gỡ khỏi cấu trúc query: `SearchandFilterCampusDto.cs`, `ViewCampusListDto.cs`, `SearchandFilterDepartmentsDto.cs`, `ViewDepartmentListDto.cs`, `ViewProfileDto.cs` (logic được gom vào `Common/` của module tương ứng).
- Bản sao trùng `docs/GUIDE CLAUDE/architecture/PROJECT_STRUCTURE_FULL.md` đã không còn (chỉ giữ bản chính tại `docs/architecture/PROJECT_STRUCTURE_FULL.md`).
- Frontend dọn bớt script scratch ở root `pems-react/` (đã bỏ nhiều `fix*.cjs`, `transform*.cjs`, `move.cjs`, `remove_obsolete.cjs`, `updateHeaders.cjs`; hiện còn `fix_mojibake.cjs`, `replace.cjs`, `replace.js`, `replace2.cjs`).
- `features/campus-management/adapters/` và `features/profile/adapters/` đã bị gỡ (campus thêm `constants.ts`, profile chuyển sang dùng `api/` + `constants/` + `components/`).

**Lưu ý đồng bộ docs / scratch artifacts (chưa sửa — chỉ ghi nhận)**
- Còn nhiều file scratch/debug ở root project (`output.json`, `output1.txt`, `output2.txt`, `payload.json`, `response.json`) và thư mục tạm `temp_hash/`, `tests/temp_bcrypt/`. Đây không phải thành phần ứng dụng; nên cân nhắc dọn dẹp ở lần sau (không xử lý trong phạm vi task này).
- `frontend/pems-react/` còn các file `out.txt`, `metadata.json`, `scratch.tsx`, `fix_mojibake.cjs`, `replace*.cjs/js` — artifacts hỗ trợ, không phải code chạy chính.
- `database/` thực tế đã rỗng phần SQL; nếu tài liệu khác vẫn trỏ tới `database/scripts/*.sql` thì cần cập nhật sang `docs/database/scripts/`.

## 5. Change Summary

- Đã quét lại toàn bộ cấu trúc thư mục từ source hiện tại (root `PEMS/`).
- Đã cập nhật lại Directory Tree theo đúng trạng thái thật, không giữ lại cấu trúc cũ đã lệch.
- Đã loại trừ các thư mục build/generated (`node_modules`, `dist`, `bin`, `obj`, `.vs`, `.git`, `.tmp-build`) — đánh dấu `[excluded]`, không mở rộng nội dung.
- Đã ghi nhận module/folder mới (DepartmentReceptionTasks, RelatedVisitors, mở rộng AgendaTemplates & Delegations/Minutes/News, các docs spec mới) và những phần cũ không còn (test scratch backend, SQL trong `database/`, một số DTO/adapters).
- Không sửa code, không rename/xóa file/folder, không tạo folder rỗng, không chạy build/migration. Chỉ cập nhật duy nhất `docs/architecture/PROJECT_STRUCTURE_FULL.md`.
