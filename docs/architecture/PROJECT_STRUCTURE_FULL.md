# PEMS Project Structure (Full Tree)

Mô tả ngắn:
- File này phản ánh cấu trúc thư mục thật hiện tại của project PEMS.
- Được cập nhật sau khi quét lại source code.
- Không bao gồm các thư mục build/generated như node_modules, dist, bin, obj.

## 1. Scope

Ghi rõ tài liệu bao gồm:
- Backend Clean Architecture
- Frontend React
- Database scripts
- Documentation
- Root configuration files

## 2. Directory Tree

```text
PEMS/
├── .claude/   [excluded]
├── .git/   [excluded]
├── .runlogs/   [excluded]
├── .vs/   [excluded]
├── .vscode/   [excluded]
├── backend/
│   ├── CheckDb/
│   │   ├── bin/   [excluded]
│   │   └── obj/   [excluded]
│   ├── JsonTest/
│   │   ├── bin/   [excluded]
│   │   ├── obj/   [excluded]
│   │   ├── JsonTest.csproj
│   │   └── Program.cs
│   ├── PEMS.Api/
│   │   ├── .tmp-build/   [excluded]
│   │   ├── App_Data/
│   │   │   └── uploads/
│   │   │       ├── email_attachment/
│   │   │       │   └── 2026/
│   │   │       │       └── 06/
│   │   │       │           └── fe8cd677dd8b4a5f975dac70ca1e8fdc.png
│   │   │       └── partner_document/
│   │   │           └── 2026/
│   │   │               └── 07/
│   │   ├── bin/   [excluded]
│   │   ├── Contracts/
│   │   │   ├── ApiResponse.cs
│   │   │   └── ApiRoutes.cs
│   │   ├── Controllers/
│   │   │   ├── AccountsController.cs
│   │   │   ├── AgendaTemplatesController.cs
│   │   │   ├── ApiIntegrationsController.cs
│   │   │   ├── AuthenticationController.cs
│   │   │   ├── BusinessCardOcrController.cs
│   │   │   ├── CalendarsController.cs
│   │   │   ├── CampusesController.cs
│   │   │   ├── DashboardController.cs
│   │   │   ├── DelegationsController.cs
│   │   │   ├── DepartmentReceptionTasksController.cs
│   │   │   ├── DepartmentsController.cs
│   │   │   ├── DocumentsController.cs
│   │   │   ├── EmailsController.cs
│   │   │   ├── EmailTemplatesController.cs
│   │   │   ├── EverAiTtsCallbackController.cs
│   │   │   ├── FaqsController.cs
│   │   │   ├── FeedbacksController.cs
│   │   │   ├── FilesController.cs
│   │   │   ├── GalleriesController.cs
│   │   │   ├── GalleryManagementTtsController.cs
│   │   │   ├── GoogleDriveOAuthController.cs
│   │   │   ├── MeetingMinutesController.cs
│   │   │   ├── NewsController.cs
│   │   │   ├── NotificationsController.cs
│   │   │   ├── PartnersController.cs
│   │   │   ├── ProfilesController.cs
│   │   │   ├── PublicContentController.cs
│   │   │   ├── PublicEmailActionsController.cs
│   │   │   ├── PublicGalleryTtsController.cs
│   │   │   ├── PublicPartnersController.cs
│   │   │   ├── PublicVisitFptuController.cs
│   │   │   ├── ReportsController.cs
│   │   │   ├── VisitInvitationsController.cs
│   │   │   ├── VisitPartnerLinksController.cs
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
│   │   ├── obj/   [excluded]
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.Development.example.json
│   │   ├── appsettings.Development.json
│   │   ├── appsettings.json
│   │   ├── appsettings.Production.json
│   │   ├── appsettings.Testing.example.json
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
│   │   │   │   ├── SetApiIntegrationStatus/
│   │   │   │   │   ├── SetApiIntegrationStatusCommand.cs
│   │   │   │   │   └── SetApiIntegrationStatusCommandHandler.cs
│   │   │   │   ├── TestApiIntegration/
│   │   │   │   │   ├── TestApiIntegrationCommand.cs
│   │   │   │   │   └── TestApiIntegrationCommandHandler.cs
│   │   │   │   ├── UpdateApiIntegrationQuota/
│   │   │   │   │   ├── UpdateApiIntegrationQuotaCommand.cs
│   │   │   │   │   ├── UpdateApiIntegrationQuotaCommandHandler.cs
│   │   │   │   │   └── UpdateApiIntegrationQuotaCommandValidator.cs
│   │   │   │   ├── UpsertGoogleDocumentAiOcrConfig/
│   │   │   │   │   ├── UpsertGoogleDocumentAiOcrConfigCommand.cs
│   │   │   │   │   ├── UpsertGoogleDocumentAiOcrConfigCommandHandler.cs
│   │   │   │   │   └── UpsertGoogleDocumentAiOcrConfigCommandValidator.cs
│   │   │   │   └── UpsertGoogleTranslationConfig/
│   │   │   │       ├── UpsertGoogleTranslationConfigCommand.cs
│   │   │   │       ├── UpsertGoogleTranslationConfigCommandHandler.cs
│   │   │   │       └── UpsertGoogleTranslationConfigCommandValidator.cs
│   │   │   ├── Common/
│   │   │   │   ├── ApiIntegrationAccess.cs
│   │   │   │   ├── ApiIntegrationConstants.cs
│   │   │   │   ├── ApiIntegrationDtos.cs
│   │   │   │   ├── ApiIntegrationMapper.cs
│   │   │   │   └── OcrProviderSettings.cs
│   │   │   └── Queries/
│   │   │       ├── GetApiIntegrationDetail/
│   │   │       │   ├── GetApiIntegrationDetailQuery.cs
│   │   │       │   └── GetApiIntegrationDetailQueryHandler.cs
│   │   │       ├── GetApiIntegrationLogs/
│   │   │       │   ├── GetApiIntegrationLogsQuery.cs
│   │   │       │   └── GetApiIntegrationLogsQueryHandler.cs
│   │   │       ├── GetApiIntegrationQuota/
│   │   │       │   ├── GetApiIntegrationQuotaQuery.cs
│   │   │       │   └── GetApiIntegrationQuotaQueryHandler.cs
│   │   │       └── GetApiIntegrations/
│   │   │           ├── GetApiIntegrationsQuery.cs
│   │   │           └── GetApiIntegrationsQueryHandler.cs
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
│   │   ├── BusinessCardOcr/
│   │   │   ├── Commands/
│   │   │   │   ├── ConfirmBusinessCardContact/
│   │   │   │   │   ├── ConfirmBusinessCardContactCommand.cs
│   │   │   │   │   ├── ConfirmBusinessCardContactCommandHandler.cs
│   │   │   │   │   └── ConfirmBusinessCardContactCommandValidator.cs
│   │   │   │   ├── DiscardBusinessCardOcrJob/
│   │   │   │   │   ├── DiscardBusinessCardOcrJobCommand.cs
│   │   │   │   │   └── DiscardBusinessCardOcrJobCommandHandler.cs
│   │   │   │   └── ScanBusinessCard/
│   │   │   │       ├── ScanBusinessCardCommand.cs
│   │   │   │       └── ScanBusinessCardCommandHandler.cs
│   │   │   ├── Common/
│   │   │   │   └── BusinessCardOcrDtos.cs
│   │   │   ├── Queries/
│   │   │   │   └── GetBusinessCardOcrJob/
│   │   │   │       ├── GetBusinessCardOcrJobQuery.cs
│   │   │   │       └── GetBusinessCardOcrJobQueryHandler.cs
│   │   │   └── Services/
│   │   │       ├── IBusinessCardOcrProvider.cs
│   │   │       ├── IBusinessCardOcrThrottle.cs
│   │   │       ├── IBusinessCardTextParser.cs
│   │   │       └── IOcrCredentialResolver.cs
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
│   │   │   │   ├── ISecretProtector.cs
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
│   │   │   ├── Options/
│   │   │   │   └── EverAiTtsOptions.cs
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
│   │   │   ├── Common/
│   │   │   │   └── StaffCalendarLogic.cs
│   │   │   └── Queries/
│   │   │       ├── GetDepartmentLeaderDashboardSummary/
│   │   │       │   ├── DepartmentLeaderDashboardSummaryDto.cs
│   │   │       │   ├── GetDepartmentLeaderDashboardSummaryQuery.cs
│   │   │       │   └── GetDepartmentLeaderDashboardSummaryQueryHandler.cs
│   │   │       ├── GetHODashboardOverview/
│   │   │       │   ├── GetHODashboardOverviewQuery.cs
│   │   │       │   ├── GetHODashboardOverviewQueryHandler.cs
│   │   │       │   └── HODashboardOverviewDto.cs
│   │   │       ├── GetStaffCalendar/
│   │   │       │   ├── GetStaffCalendarQuery.cs
│   │   │       │   ├── GetStaffCalendarQueryHandler.cs
│   │   │       │   └── StaffCalendarDtos.cs
│   │   │       └── GetStaffCalendarDetail/
│   │   │           ├── GetStaffCalendarDetailQuery.cs
│   │   │           ├── GetStaffCalendarDetailQueryHandler.cs
│   │   │           └── StaffCalendarDetailDto.cs
│   │   ├── Delegations/
│   │   │   ├── Commands/
│   │   │   │   ├── ApproveCampusInstance/
│   │   │   │   │   ├── ApproveCampusInstanceCommand.cs
│   │   │   │   │   ├── ApproveCampusInstanceCommandHandler.cs
│   │   │   │   │   ├── ApproveCampusInstanceCommandValidator.cs
│   │   │   │   │   └── ApproveCampusInstanceResponse.cs
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
│   │   │   │   ├── ProposeResourceModification/
│   │   │   │   │   ├── ProposeResourceModificationCommand.cs
│   │   │   │   │   ├── ProposeResourceModificationCommandHandler.cs
│   │   │   │   │   ├── ProposeResourceModificationCommandValidator.cs
│   │   │   │   │   └── ProposeResourceModificationResponse.cs
│   │   │   │   ├── RejectCampusInstance/
│   │   │   │   │   ├── RejectCampusInstanceCommand.cs
│   │   │   │   │   ├── RejectCampusInstanceCommandHandler.cs
│   │   │   │   │   ├── RejectCampusInstanceCommandValidator.cs
│   │   │   │   │   └── RejectCampusInstanceResponse.cs
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
│   │   │   │   ├── ResubmitRejectedVisitRequest/
│   │   │   │   │   ├── ResubmitRejectedVisitRequestCommand.cs
│   │   │   │   │   ├── ResubmitRejectedVisitRequestCommandHandler.cs
│   │   │   │   │   ├── ResubmitRejectedVisitRequestCommandValidator.cs
│   │   │   │   │   └── ResubmitRejectedVisitRequestResponse.cs
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
│   │   │   │   ├── UpdatePendingVisitRequest/
│   │   │   │   │   ├── UpdatePendingVisitRequestCommand.cs
│   │   │   │   │   ├── UpdatePendingVisitRequestCommandHandler.cs
│   │   │   │   │   ├── UpdatePendingVisitRequestCommandValidator.cs
│   │   │   │   │   └── UpdatePendingVisitRequestResponse.cs
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
│   │   │   │   ├── GetEditableVisitRequestDetail/
│   │   │   │   │   ├── EditableVisitRequestDetailDto.cs
│   │   │   │   │   ├── GetEditableVisitRequestDetailQuery.cs
│   │   │   │   │   └── GetEditableVisitRequestDetailQueryHandler.cs
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
│   │   │   ├── Rules/
│   │   │   │   └── README.md
│   │   │   └── Services/
│   │   │       └── VisitRequestAggregateStatusService.cs
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
│   │   │   ├── Commands/
│   │   │   │   └── SubmitVisitFeedback/
│   │   │   │       ├── SubmitVisitFeedbackCommand.cs
│   │   │   │       ├── SubmitVisitFeedbackCommandHandler.cs
│   │   │   │       └── SubmitVisitFeedbackCommandValidator.cs
│   │   │   ├── Common/
│   │   │   │   ├── FeedbackConstants.cs
│   │   │   │   ├── FeedbackEligibility.cs
│   │   │   │   ├── FeedbackRules.cs
│   │   │   │   └── FeedbackTargetDto.cs
│   │   │   └── Queries/
│   │   │       ├── GetPendingFeedbackNotifications/
│   │   │       │   ├── GetPendingFeedbackNotificationsQuery.cs
│   │   │       │   └── GetPendingFeedbackNotificationsQueryHandler.cs
│   │   │       ├── GetVisitFeedbackTargets/
│   │   │       │   ├── GetVisitFeedbackTargetsQuery.cs
│   │   │       │   └── GetVisitFeedbackTargetsQueryHandler.cs
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
│   │   │   │   ├── GalleryCoverImage.cs
│   │   │   │   ├── GalleryDetailBuilder.cs
│   │   │   │   ├── GalleryErrorCodes.cs
│   │   │   │   ├── GalleryFileUrls.cs
│   │   │   │   ├── GalleryItemDetailDto.cs
│   │   │   │   ├── GalleryItemListItemDto.cs
│   │   │   │   ├── GalleryItemListQueryExecutor.cs
│   │   │   │   ├── GalleryItemTypes.cs
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
│   │   │   │       ├── GetPublicCampuses/
│   │   │   │       │   ├── GetPublicCampusesQuery.cs
│   │   │   │       │   └── GetPublicCampusesQueryHandler.cs
│   │   │   │       ├── GetPublicCampusNavigation/
│   │   │   │       │   ├── GetPublicCampusNavigationQuery.cs
│   │   │   │       │   └── GetPublicCampusNavigationQueryHandler.cs
│   │   │   │       ├── GetPublicGalleryItemDetail/
│   │   │   │       │   ├── GetPublicGalleryItemDetailQuery.cs
│   │   │   │       │   └── GetPublicGalleryItemDetailQueryHandler.cs
│   │   │   │       ├── GetPublicGalleryMedia/
│   │   │   │       │   ├── GetPublicGalleryMediaQuery.cs
│   │   │   │       │   └── GetPublicGalleryMediaQueryHandler.cs
│   │   │   │       ├── GetPublicLocationGalleryItem/
│   │   │   │       │   ├── GetPublicLocationGalleryItemQuery.cs
│   │   │   │       │   └── GetPublicLocationGalleryItemQueryHandler.cs
│   │   │   │       └── GetPublicLocationShowcase/
│   │   │   │           ├── GetPublicLocationShowcaseQuery.cs
│   │   │   │           └── GetPublicLocationShowcaseQueryHandler.cs
│   │   │   ├── Queries/
│   │   │   │   ├── GetGalleryFilterOptions/
│   │   │   │   │   ├── GetGalleryFilterOptionsQuery.cs
│   │   │   │   │   └── GetGalleryFilterOptionsQueryHandler.cs
│   │   │   │   ├── SearchGalleryItems/
│   │   │   │   │   ├── SearchGalleryItemsQuery.cs
│   │   │   │   │   └── SearchGalleryItemsQueryHandler.cs
│   │   │   │   ├── ViewGalleryItemDetails/
│   │   │   │   │   ├── ViewGalleryItemDetailsQuery.cs
│   │   │   │   │   └── ViewGalleryItemDetailsQueryHandler.cs
│   │   │   │   ├── ViewGalleryItemList/
│   │   │   │   │   ├── ViewGalleryItemListQuery.cs
│   │   │   │   │   └── ViewGalleryItemListQueryHandler.cs
│   │   │   │   └── ViewGalleryLocationList/
│   │   │   │       ├── ViewGalleryLocationListQuery.cs
│   │   │   │       └── ViewGalleryLocationListQueryHandler.cs
│   │   │   └── Tts/
│   │   │       ├── Commands/
│   │   │       │   ├── EnsurePublicGalleryItemTtsAudio/
│   │   │       │   │   ├── EnsurePublicGalleryItemTtsAudioCommand.cs
│   │   │       │   │   └── EnsurePublicGalleryItemTtsAudioCommandHandler.cs
│   │   │       │   └── RegenerateGalleryItemTtsAudio/
│   │   │       │       ├── RegenerateGalleryItemTtsAudioCommand.cs
│   │   │       │       └── RegenerateGalleryItemTtsAudioCommandHandler.cs
│   │   │       ├── Queries/
│   │   │       │   ├── GetGalleryItemTtsStatus/
│   │   │       │   │   ├── GetGalleryItemTtsStatusQuery.cs
│   │   │       │   │   └── GetGalleryItemTtsStatusQueryHandler.cs
│   │   │       │   └── GetPublicGalleryItemTtsAudioStatus/
│   │   │       │       ├── GetPublicGalleryItemTtsAudioStatusQuery.cs
│   │   │       │       └── GetPublicGalleryItemTtsAudioStatusQueryHandler.cs
│   │   │       ├── EverAiTtsModels.cs
│   │   │       ├── GalleryItemTtsAudioResponse.cs
│   │   │       ├── GalleryItemTtsService.cs
│   │   │       ├── GalleryTtsConstants.cs
│   │   │       ├── GalleryTtsHashService.cs
│   │   │       ├── IEverAiTtsClient.cs
│   │   │       ├── IGalleryItemTtsService.cs
│   │   │       ├── IGalleryTtsHashService.cs
│   │   │       └── IGalleryTtsJobQueue.cs
│   │   ├── MeetingMinutes/
│   │   │   └── Queries/
│   │   │       ├── ExportMinutes/
│   │   │       │   ├── ExportMinutesExcelQueryHandler.cs
│   │   │       │   ├── ExportMinutesPdfQueryHandler.cs
│   │   │       │   └── ExportMinutesQuery.cs
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
│   │   │   │   ├── TranslateNews/
│   │   │   │   │   ├── TranslateNewsCommand.cs
│   │   │   │   │   └── TranslateNewsCommandHandler.cs
│   │   │   │   └── UploadNewsCoverImage/
│   │   │   │       ├── UploadNewsCoverImageCommand.cs
│   │   │   │       ├── UploadNewsCoverImageCommandHandler.cs
│   │   │   │       └── UploadNewsCoverImageResponse.cs
│   │   │   ├── Queries/
│   │   │   │   ├── GetEligibleVisitInstancesForNews/
│   │   │   │   │   ├── GetEligibleVisitInstancesForNewsDto.cs
│   │   │   │   │   ├── GetEligibleVisitInstancesForNewsQuery.cs
│   │   │   │   │   └── GetEligibleVisitInstancesForNewsQueryHandler.cs
│   │   │   │   ├── ViewNewsDetails/
│   │   │   │   │   ├── ViewNewsDetailsDto.cs
│   │   │   │   │   ├── ViewNewsDetailsQuery.cs
│   │   │   │   │   └── ViewNewsDetailsQueryHandler.cs
│   │   │   │   └── ViewNewsList/
│   │   │   │       ├── ViewNewsListDto.cs
│   │   │   │       ├── ViewNewsListQuery.cs
│   │   │   │       ├── ViewNewsListQueryHandler.cs
│   │   │   │       └── ViewNewsListQueryValidator.cs
│   │   │   └── Services/
│   │   │       └── INewsTranslationService.cs
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
│   │   ├── obj/   [excluded]
│   │   ├── Partners/
│   │   │   ├── Aliases/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CreatePartnerAlias/
│   │   │   │   │   │   ├── CreatePartnerAliasCommand.cs
│   │   │   │   │   │   ├── CreatePartnerAliasCommandHandler.cs
│   │   │   │   │   │   └── CreatePartnerAliasCommandValidator.cs
│   │   │   │   │   └── DeactivatePartnerAlias/
│   │   │   │   │       ├── DeactivatePartnerAliasCommand.cs
│   │   │   │   │       └── DeactivatePartnerAliasCommandHandler.cs
│   │   │   │   └── Queries/
│   │   │   │       └── GetPartnerAliases/
│   │   │   │           ├── GetPartnerAliasesQuery.cs
│   │   │   │           └── GetPartnerAliasesQueryHandler.cs
│   │   │   ├── Commands/
│   │   │   │   ├── ApprovePartner/
│   │   │   │   │   ├── ApprovePartnerCommand.cs
│   │   │   │   │   └── ApprovePartnerCommandHandler.cs
│   │   │   │   ├── CreatePartner/
│   │   │   │   │   ├── CreatePartnerCommand.cs
│   │   │   │   │   ├── CreatePartnerCommandHandler.cs
│   │   │   │   │   └── CreatePartnerCommandValidator.cs
│   │   │   │   ├── CreatePartnerFromGuest/
│   │   │   │   │   ├── CreatePartnerFromGuestCommand.cs
│   │   │   │   │   └── CreatePartnerFromGuestCommandHandler.cs
│   │   │   │   ├── RejectPartner/
│   │   │   │   │   ├── RejectPartnerCommand.cs
│   │   │   │   │   ├── RejectPartnerCommandHandler.cs
│   │   │   │   │   └── RejectPartnerCommandValidator.cs
│   │   │   │   └── UpdatePartner/
│   │   │   │       ├── UpdatePartnerCommand.cs
│   │   │   │       ├── UpdatePartnerCommandHandler.cs
│   │   │   │       └── UpdatePartnerCommandValidator.cs
│   │   │   ├── Common/
│   │   │   │   ├── PartnerAccess.cs
│   │   │   │   ├── PartnerConstants.cs
│   │   │   │   ├── PartnerDtos.cs
│   │   │   │   ├── PartnerMatcher.cs
│   │   │   │   └── PartnerNormalization.cs
│   │   │   ├── Contacts/
│   │   │   │   ├── Commands/
│   │   │   │   │   ├── CreatePartnerContact/
│   │   │   │   │   │   ├── CreatePartnerContactCommand.cs
│   │   │   │   │   │   ├── CreatePartnerContactCommandHandler.cs
│   │   │   │   │   │   └── CreatePartnerContactCommandValidator.cs
│   │   │   │   │   ├── DeactivatePartnerContact/
│   │   │   │   │   │   ├── DeactivatePartnerContactCommand.cs
│   │   │   │   │   │   └── DeactivatePartnerContactCommandHandler.cs
│   │   │   │   │   ├── SetPrimaryPartnerContact/
│   │   │   │   │   │   ├── SetPrimaryPartnerContactCommand.cs
│   │   │   │   │   │   └── SetPrimaryPartnerContactCommandHandler.cs
│   │   │   │   │   └── UpdatePartnerContact/
│   │   │   │   │       ├── UpdatePartnerContactCommand.cs
│   │   │   │   │       ├── UpdatePartnerContactCommandHandler.cs
│   │   │   │   │       └── UpdatePartnerContactCommandValidator.cs
│   │   │   │   ├── Common/
│   │   │   │   │   └── PartnerContactWriteSupport.cs
│   │   │   │   └── Queries/
│   │   │   │       └── GetPartnerContacts/
│   │   │   │           ├── GetPartnerContactsQuery.cs
│   │   │   │           └── GetPartnerContactsQueryHandler.cs
│   │   │   ├── Documents/
│   │   │   │   ├── Commands/
│   │   │   │   │   └── UploadPartnerDocument/
│   │   │   │   │       ├── UploadPartnerDocumentCommand.cs
│   │   │   │   │       ├── UploadPartnerDocumentCommandHandler.cs
│   │   │   │   │       └── UploadPartnerDocumentCommandValidator.cs
│   │   │   │   └── Queries/
│   │   │   │       └── GetPartnerDocuments/
│   │   │   │           ├── GetPartnerDocumentsQuery.cs
│   │   │   │           └── GetPartnerDocumentsQueryHandler.cs
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── PartnersMappingProfile.cs
│   │   │   ├── Queries/
│   │   │   │   ├── GetPartnerDetail/
│   │   │   │   │   ├── GetPartnerDetailQuery.cs
│   │   │   │   │   └── GetPartnerDetailQueryHandler.cs
│   │   │   │   ├── GetPartners/
│   │   │   │   │   ├── GetPartnersQuery.cs
│   │   │   │   │   └── GetPartnersQueryHandler.cs
│   │   │   │   ├── GetPendingPartnerApprovals/
│   │   │   │   │   ├── GetPendingPartnerApprovalsQuery.cs
│   │   │   │   │   └── GetPendingPartnerApprovalsQueryHandler.cs
│   │   │   │   ├── GetPublicPartnerCountries/
│   │   │   │   │   ├── GetPublicPartnerCountriesQuery.cs
│   │   │   │   │   └── GetPublicPartnerCountriesQueryHandler.cs
│   │   │   │   ├── GetPublicPartnerDetail/
│   │   │   │   │   ├── GetPublicPartnerDetailQuery.cs
│   │   │   │   │   └── GetPublicPartnerDetailQueryHandler.cs
│   │   │   │   ├── GetPublicPartnerMedia/
│   │   │   │   │   ├── GetPublicPartnerMediaQuery.cs
│   │   │   │   │   └── GetPublicPartnerMediaQueryHandler.cs
│   │   │   │   ├── GetPublicPartners/
│   │   │   │   │   ├── GetPublicPartnersQuery.cs
│   │   │   │   │   └── GetPublicPartnersQueryHandler.cs
│   │   │   │   ├── GetPublicPartnerTypes/
│   │   │   │   │   ├── GetPublicPartnerTypesQuery.cs
│   │   │   │   │   └── GetPublicPartnerTypesQueryHandler.cs
│   │   │   │   ├── MatchPartner/
│   │   │   │   │   ├── MatchPartnerQuery.cs
│   │   │   │   │   └── MatchPartnerQueryHandler.cs
│   │   │   │   └── SearchPublicPartnerOptions/
│   │   │   │       ├── PublicPartnerOptionDto.cs
│   │   │   │       ├── SearchPublicPartnerOptionsQuery.cs
│   │   │   │       ├── SearchPublicPartnerOptionsQueryHandler.cs
│   │   │   │       └── SearchPublicPartnerOptionsQueryValidator.cs
│   │   │   ├── Rules/
│   │   │   │   └── README.md
│   │   │   └── VisitLinks/
│   │   │       ├── Commands/
│   │   │       │   ├── CreateOrUpdateVisitGuestPartnerLink/
│   │   │       │   │   ├── CreateOrUpdateVisitGuestPartnerLinkCommand.cs
│   │   │       │   │   └── CreateOrUpdateVisitGuestPartnerLinkCommandHandler.cs
│   │   │       │   └── RejectVisitGuestPartnerSuggestion/
│   │   │       │       ├── RejectVisitGuestPartnerSuggestionCommand.cs
│   │   │       │       └── RejectVisitGuestPartnerSuggestionCommandHandler.cs
│   │   │       ├── Common/
│   │   │       │   └── VisitLinkSupport.cs
│   │   │       └── Queries/
│   │   │           └── GetVisitGuestPartnerLinks/
│   │   │               ├── GetVisitGuestPartnerLinksQuery.cs
│   │   │               └── GetVisitGuestPartnerLinksQueryHandler.cs
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
│   │   │   │   ├── GetFaqTypeCounts/
│   │   │   │   │   ├── GetFaqTypeCountsQuery.cs
│   │   │   │   │   └── GetFaqTypeCountsQueryHandler.cs
│   │   │   │   ├── GetPublicNewsFile/
│   │   │   │   │   ├── GetPublicNewsFileQuery.cs
│   │   │   │   │   └── GetPublicNewsFileQueryHandler.cs
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
│   │   │   │   ├── ExportDeptLeaderInvoice/
│   │   │   │   │   ├── ExportDeptLeaderInvoiceCommand.cs
│   │   │   │   │   └── ExportDeptLeaderInvoiceCommandHandler.cs
│   │   │   │   ├── ExportDeptLeaderReport/
│   │   │   │   │   ├── ExportDeptLeaderReportCommand.cs
│   │   │   │   │   └── ExportDeptLeaderReportCommandHandler.cs
│   │   │   │   ├── ExportHoReport/
│   │   │   │   │   ├── ExportHoReportCommand.cs
│   │   │   │   │   └── ExportHoReportCommandHandler.cs
│   │   │   │   ├── ExportStaffLeaderReport/
│   │   │   │   │   ├── ExportStaffLeaderReportCommand.cs
│   │   │   │   │   └── ExportStaffLeaderReportCommandHandler.cs
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
│   │   │       ├── GetDeptLeaderInvoiceData/
│   │   │       │   ├── GetDeptLeaderInvoiceItemsQuery.cs
│   │   │       │   ├── GetDeptLeaderInvoiceItemsQueryHandler.cs
│   │   │       │   ├── GetDeptLeaderInvoiceVisitsQuery.cs
│   │   │       │   └── GetDeptLeaderInvoiceVisitsQueryHandler.cs
│   │   │       ├── GetDeptLeaderReportOverview/
│   │   │       │   ├── DeptLeaderReportOverviewDto.cs
│   │   │       │   ├── GetDeptLeaderReportOverviewQuery.cs
│   │   │       │   └── GetDeptLeaderReportOverviewQueryHandler.cs
│   │   │       ├── GetHoReportOverview/
│   │   │       │   ├── GetHoReportOverviewQuery.cs
│   │   │       │   ├── GetHoReportOverviewQueryHandler.cs
│   │   │       │   └── HoReportOverviewDto.cs
│   │   │       ├── GetStaffLeaderReportOverview/
│   │   │       │   ├── GetStaffLeaderReportOverviewQuery.cs
│   │   │       │   ├── GetStaffLeaderReportOverviewQueryHandler.cs
│   │   │       │   └── StaffLeaderReportOverviewDto.cs
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
│   │   │   │   ├── ApiUsageQuota.cs
│   │   │   │   └── BusinessCardOcrJob.cs
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
│   │   │   │   ├── GalleryItemTtsAudio.cs
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
│   │   │   │   ├── PartnerAlias.cs
│   │   │   │   ├── Partnercontact.cs
│   │   │   │   └── VisitGuestPartnerLink.cs
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
│       ├── BackgroundJobs/
│       │   ├── GalleryTtsBackgroundService.cs
│       │   └── VisitReminderDispatchHostedService.cs
│       ├── bin/   [excluded]
│       ├── Common/
│       │   └── DateTimeService.cs
│       ├── Email/
│       │   ├── EmailActionTokenService.cs
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
│       │   ├── Ocr/
│       │   │   └── OcrService.cs
│       │   └── Tts/
│       │       └── EverAiTtsClient.cs
│       ├── FileStorage/
│       │   ├── GoogleDrive/
│       │   │   ├── GoogleDriveFolderResolver.cs
│       │   │   └── GoogleDriveStorageService.cs
│       │   ├── CloudFileStorageService.cs
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
│       ├── Ocr/
│       │   ├── BusinessCardTextParser.cs
│       │   ├── GoogleDocumentAiBusinessCardOcrProvider.cs
│       │   ├── GoogleServiceAccountTokenProvider.cs
│       │   ├── InMemoryBusinessCardOcrThrottle.cs
│       │   └── OcrCredentialResolver.cs
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
│       │   ├── AesGcmSecretProtector.cs
│       │   └── HtmlSanitizerService.cs
│       ├── Services/
│       │   ├── ApprovalRoutingService.cs
│       │   ├── UserProvisionService.cs
│       │   └── VisitRequestService.cs
│       ├── Translation/
│       │   └── GoogleNewsTranslationService.cs
│       ├── DependencyInjection.cs
│       └── PEMS.Infrastructure.csproj
├── docs/
│   ├── account-management/
│   │   ├── PROMPT_UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER_PEMS.md
│   │   ├── UC_StaffLeader_Related_Visitor_Accounts_Tab.md
│   │   └── UC95_UC99_ACCOUNT_LIST_SEARCH_FILTER.md
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
│   ├── CampusManagement/
│   │   ├── 00_CAMPUS_MANAGEMENT_COMMON_RULES_HO.md
│   │   ├── 01_UC82_VIEW_CAMPUS_LIST_HO.md
│   │   ├── 02_UC83_SEARCH_FILTER_CAMPUS_HO.md
│   │   ├── 03_UC81_CREATE_CAMPUS_HO.md
│   │   ├── 04_UC84_VIEW_CAMPUS_DETAILS_HO.md
│   │   ├── 05_UC85_UPDATE_CAMPUS_HO.md
│   │   └── 06_UC86_MANAGE_CAMPUS_STATUS_HO.md
│   ├── ChangeSauHopChiQUyen/
│   │   ├── PEMS_CAMPUS_INDEPENDENT_APPROVAL_IMPLEMENTATION_PLAN.md
│   │   ├── PEMS_VISITOR_EDIT_RESUBMIT_CANCEL24_IMPLEMENTATION_PLAN.md
│   │   └── PEMS_VISITOR_EDIT_RESUBMIT_CANCEL24_IMPLEMENTATION_REPORT.md
│   ├── dashboard/
│   │   ├── PEMS_HO_Dashboard_Redesign_Prompt.md
│   │   ├── PROMPT_DASHBOARD_CALENDAR_STAFF_STAFF_LEADER.md
│   │   └── PROMPT_PUBLIC_FAQ_PAGE_REDESIGN_PEMS.md
│   ├── database/
│   │   ├── scripts/
│   │   │   ├── DbSeeder/
│   │   │   │   ├── bin/   [excluded]
│   │   │   │   └── obj/   [excluded]
│   │   │   └── pems_full_v10_TTS_Gallery_FULL_UPDATED.sql
│   │   ├── Table/
│   │   │   └── PEMS_v10_NEW_FINAL_SQL_TABLE_FIELD_DICTIONARY_MATCHED.docx
│   │   ├── DATABASE_SCHEMA_v8_4_refined_v6_v10_no_dynamic_permissions_FULL_UPDATED.md
│   │   └── PROMPT_UPDATE_CODE_FOR_SQL_V10_PEMS.md
│   ├── delegation/
│   │   ├── invitation/
│   │   │   └── PROMPT_UPDATE_STUDENT_STAFF_INVITATION_CONTRIBUTION.md
│   │   ├── processFormByHost/
│   │   │   └── UC_HOST_VISIT_PROCESS_INVITATION_EMAIL_FLOW.md
│   │   ├── setup delegation/
│   │   │   └── PEMS_VISIT_DETAIL_PROCESS_LOGIC_REQUIREMENTS.md
│   │   ├── status/
│   │   │   └── PEMS_VISIT_LIFECYCLE_LOGISTICS_STATUS_REQUIREMENTS.md
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
│   ├── Department/
│   │   ├── PEMS_DEPARTMENT_PERSONNEL_SHORT_FUNCTION_PROMPT.md
│   │   ├── PEMS_DEPT_LEADER_ASSIGNMENT_PROGRESS_UNIFIED_PROMPT.md
│   │   ├── PEMS_DEPT_LEADER_LOGISTICS_ASSIGNMENT_FLOW_PROMPT.md
│   │   ├── PEMS_DEPT_LEADER_STATUS_LOGIC_UPDATE_PROMPT.md
│   │   ├── PEMS_DEPT_RECEPTION_TASKS_REAL_DATA_PROMPT.md
│   │   ├── PEMS_DEPTLEAD_DASHBOARD_EMAIL_LOCAL_DRAFT_PROMPT.md
│   │   ├── PEMS_DEPTLEAD_UI_RESTORE_ACTIONS_PROMPT - Copy.md
│   │   ├── PEMS_DEPTLEAD_UI_RESTORE_ACTIONS_PROMPT.md
│   │   ├── PEMS_EMAIL_MANAGEMENT_REAL_DATA_WORKFLOW_PROMPT.md
│   │   └── PROMPT_FIX_DEPT_STAFF_ASSIGNED_TASKS_UI_FLOW_PEMS.md
│   ├── Department_Staff_Leader/
│   │   ├── UC-101_ADD_NEW_DEPARTMENT_STAFF_LEADER.md
│   │   ├── UC-102_UPDATE_DEPARTMENT_STAFF_LEADER.md
│   │   ├── UC-103_SEARCH_FILTER_DEPARTMENTS_STAFF_LEADER.md
│   │   ├── UC-104_VIEW_DEPARTMENT_LIST_STAFF_LEADER.md
│   │   ├── UC-105_VIEW_DEPARTMENT_DETAILS_STAFF_LEADER.md
│   │   └── UC-106_MANAGE_DEPARTMENT_STATUS_STAFF_LEADER.md
│   ├── document/
│   │   ├── PROMPT_DOCUMENT_DETAIL_DYNAMIC_BY_OWNER_TYPE.md
│   │   ├── PROMPT_FIX_DOCUMENT_MANAGEMENT_PAGE_PEMS.md
│   │   └── PROMPT_STAFF_LEADER_DOCUMENT_MANAGEMENT_CAMPUS_SCOPE.md
│   ├── feedback/
│   │   ├── FEEDBACK_MANAGEMENT_UI_PROPOSAL_AND_AI_PROMPT.md
│   │   ├── PROMPT_FEEDBACK_HANDOVER_UI_COMPACT_PEMS.md
│   │   ├── PROMPT_FIX_FEEDBACK_MANAGEMENT_PAGE_PEMS.md
│   │   └── PROMPT_FIX_FEEDBACK_MANAGEMENT_SCOPE_FILTER_DETAIL.md
│   ├── GalleryManagement/
│   │   ├── PROMPT_EVERAI_TTS_GALLERY_ITEM_INTEGRATION.md
│   │   ├── PROMPT_GALLERY_GOOGLE_DRIVE_FOLDER_ROUTING_ONLY.md
│   │   ├── PROMPT_UI_PUBLIC_GALLERY_AREA_SHOWCASE.md
│   │   ├── PROMPT_UI_PUBLIC_GALLERY_LOCATION_SHOWCASE_MEDIA_AND_DELEGATION.md
│   │   ├── PROMPT_UPDATE_GALLERY_ALLOW_MULTIPLE_ITEMS_PER_LOCATION.md
│   │   ├── PROMPT_UPDATE_GALLERY_AREA_LOCATION_COVER_AND_ITEM_TYPE.md
│   │   ├── UC_Public_VisitFPTU_Gallery.md
│   │   ├── UC_Quan_Ly_Khu_Vuc_Gallery_UPDATED_FINAL.md
│   │   └── UC_Quan_Ly_VisitFPTU_Gallery.md
│   ├── GoogleDrive/
│   │   ├── PEMS_GOOGLE_DRIVE_OAUTH_REFRESH_TOKEN_FLOW.md
│   │   ├── PEMS_GOOGLE_DRIVE_STORAGE_FOUNDATION_REFACTOR_FOR_FUTURE_UPLOADS.md
│   │   └── PEMS_GOOGLE_DRIVE_UPLOAD_FOUNDATION_DONE_AND_HOWTO.md
│   ├── GUIDE CLAUDE/
│   │   ├── architecture/
│   │   │   └── CLEAN_ARCHITECTURE.md
│   │   ├── FRONTEND/
│   │   │   └── PEMS_UI_DESIGN_SYSTEM_PROMPT.md
│   │   └── PEMS_CLAUDE_PROJECT_INSTRUCTIONS_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── homepage/
│   │   └── PROMPT_ROLE_AWARE_HOMEPAGE_PEMS.md
│   ├── minute/
│   │   └── PROMPT_FIX_MINUTES_MANAGEMENT_SCOPE_FILTER_DETAIL_PDF.md
│   ├── News-Canh/
│   │   ├── PEMS_NEWS_FULL_IMPLEMENTATION_PLAN.md
│   │   └── PROMPT_PUBLIC_NEWS_PAGE_REDESIGN_PEMS.md
│   ├── PARTNER_canh/
│   │   ├── 00_PEMS_PARTNER_AND_OCR_MASTER_PLAN.md
│   │   ├── 01_PEMS_PARTNER_MODULE_FULL_PROMPT.md
│   │   ├── 02_PEMS_BUSINESS_CARD_OCR_API_CONFIG_PROMPT.md
│   │   └── PROMPT_PUBLIC_PARTNERS_PAGE_REDESIGN_PEMS.md
│   ├── permissions/
│   │   ├── PERMISSION_MATRIX.md
│   │   └── PERMISSION_RULES.md
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
│   ├── prompt_test/
│   │   ├── PROMPT_AI_CREATE_FAQ_UNIT_INTEGRATION_SAFE_NO_DOCKER.md
│   │   ├── PROMPT_AI_UC62_VIEW_LIST_FAQ_HO_UNIT_INTEGRATION_SAFE_NO_DOCKER_v1.md
│   │   ├── PROMPT_AI_UPDATE_FAQ_UNIT_INTEGRATION_SAFE_NO_DOCKER_v3_BEST_PRACTICE.md
│   │   └── Require_UC_CreateFAQ.md
│   ├── Prompt_usecase/
│   │   ├── PROMPT_CODE_CREATE_NEWS_BACKEND.md
│   │   ├── PROMPT_CODE_UC05_VIEW_FAQ_BACKEND_UPDATED_PROJECT_STRUCTURE.md
│   │   ├── PROMPT_CODE_UC62_VIEW_LIST_FAQ_BACKEND.md
│   │   ├── PROMPT_CODE_UC63_CREATE_FAQ_BACKEND.md
│   │   ├── PROMPT_CODE_UC64_UPDATE_FAQ_BACKEND.md
│   │   └── PROMPT_CODE_UC88_VIEW_NEWS_LIST_BACKEND.md
│   ├── report/
│   │   ├── PEMS_DepartmentLeader_Report_AI_Code_Prompt (1).md
│   │   ├── PEMS_HO_Report_AI_Code_Prompt.md
│   │   ├── PEMS_StaffLeader_Report_AI_Code_Prompt.md
│   │   └── PEMS_StaffLeader_Report_Fix_Prompt.md
│   ├── send email dep/
│   │   ├── department_task_logistics_email_token_flow_requirements.md
│   │   └── PROMPT_UPDATE_LOGISTICS_FRONTEND_EMAIL_SQL_V10.md
│   ├── swimlane/
│   ├── testing/
│   │   └── CREATE_TEST_DATABASE.md
│   ├── todo/
│   │   └── PEMS_AUTH_NEWS_SECURITY_TODO.md
│   ├── use-cases/
│   │   ├── USE_CASE_LIST.md
│   │   └── USE_CASE_NOTES_HO_VIEW_SINGLE_READONLY.md
│   ├── PEMS_CANONICAL_BUSINESS_RULES_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── PEMS_UC_IMPLEMENTATION_RULEBOOK_FRONTEND_BACKEND_DATABASE_VALIDATION_SECURITY_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── PROJECT_OVERVIEW_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── PROMPT_STANDARDIZE_ROLE_SUBROLE_DEPARTMENT_v8_4_refined_v6_v10_FULL_UPDATED.md
│   ├── Report 5.2_L1-UnitTests_Template.xlsx
│   ├── Report 5.2_L2-IntegrationTests_Template.xlsx
│   └── VISITOR_MANAGEMENT_SYSTEM_v8_4_refined_v6_v10_FULL_UPDATED.md
├── frontend/
│   └── pems-react/
│       ├── dist/   [excluded]
│       ├── node_modules/   [excluded]
│       ├── scratch/
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
│       │   │   │   ├── logo_fpt_remove_bg.png
│       │   │   │   ├── LogoFPT-2017-copy-3042-1513928399_1200x0.jpg
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
│       │   │   └── ne_110m_admin_0_countries.geojson
│       │   ├── components/
│       │   │   ├── dashboard/
│       │   │   │   ├── NotificationBell.tsx
│       │   │   │   └── Sidebar.tsx
│       │   │   ├── home/
│       │   │   │   ├── internal/
│       │   │   │   │   ├── GuideStepsSection.tsx
│       │   │   │   │   ├── InternalFinalCta.tsx
│       │   │   │   │   ├── QuickAccessSection.tsx
│       │   │   │   │   └── WelcomeHero.tsx
│       │   │   │   ├── AboutFptuSection.tsx
│       │   │   │   ├── CampusShowcaseSection.tsx
│       │   │   │   ├── FaqPreviewSection.tsx
│       │   │   │   ├── FinalCtaSection.tsx
│       │   │   │   ├── GalleryPreviewSection.tsx
│       │   │   │   ├── GlobeShowcase.tsx
│       │   │   │   ├── HeroSection.tsx
│       │   │   │   ├── LazyGlobeShowcase.tsx
│       │   │   │   ├── NewsSection.tsx
│       │   │   │   ├── PartnersSection.tsx
│       │   │   │   └── VisitProcessSection.tsx
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
│       │   │   ├── business-card-ocr/
│       │   │   │   ├── api/
│       │   │   │   │   └── businessCardOcrApi.ts
│       │   │   │   ├── components/
│       │   │   │   │   └── BusinessCardScanModal.tsx
│       │   │   │   └── types/
│       │   │   │       └── businessCardOcr.types.ts
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
│       │   │   │   │   ├── departmentLeaderDashboardApi.ts
│       │   │   │   │   └── staffCalendarApi.ts
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
│       │   │   │   │   ├── SubmittedVisitRequestInfoPanel.tsx
│       │   │   │   │   └── VisitNewsPostList.tsx
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
│       │   │   │   ├── components/
│       │   │   │   │   ├── DocumentFilterBar.tsx
│       │   │   │   │   ├── DocumentSummaryCompact.tsx
│       │   │   │   │   └── DocumentTable.tsx
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
│       │   │   │   │   ├── feedbacksApi.ts
│       │   │   │   │   └── visitFeedbackApi.ts
│       │   │   │   ├── components/
│       │   │   │   │   ├── CommentModal.tsx
│       │   │   │   │   ├── CompactStarRating.tsx
│       │   │   │   │   ├── FeedbackFilterBar.tsx
│       │   │   │   │   ├── FeedbackGroupSection.tsx
│       │   │   │   │   ├── FeedbackRatingStars.tsx
│       │   │   │   │   ├── FeedbackSummaryCompact.tsx
│       │   │   │   │   ├── FeedbackTable.tsx
│       │   │   │   │   ├── FeedbackTargetRow.tsx
│       │   │   │   │   ├── FeedbackTypeSection.tsx
│       │   │   │   │   └── VisitFeedbackModal.tsx
│       │   │   │   ├── constants/
│       │   │   │   │   └── feedbackTypes.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   ├── useFeedbacks.ts
│       │   │   │   │   └── useVisitFeedback.ts
│       │   │   │   └── types/
│       │   │   │       ├── feedbacks.types.ts
│       │   │   │       └── visitFeedback.types.ts
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
│       │   │   │   ├── components/
│       │   │   │   │   ├── CreatePartnerFromParticipantModal.tsx
│       │   │   │   │   └── ParticipantPartnerCell.tsx
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
│       │   │   ├── public-faq/
│       │   │   │   ├── api/
│       │   │   │   │   └── publicFaqApi.ts
│       │   │   │   └── types/
│       │   │   │       └── publicFaq.types.ts
│       │   │   ├── public-partners/
│       │   │   │   ├── api/
│       │   │   │   │   └── publicPartnersApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── usePublicPartnerImage.ts
│       │   │   │   ├── types/
│       │   │   │   │   └── publicPartners.types.ts
│       │   │   │   └── utils/
│       │   │   │       ├── countryFlag.ts
│       │   │   │       └── countryMatch.ts
│       │   │   ├── public-search/
│       │   │   │   ├── api/
│       │   │   │   │   └── publicSearchApi.ts
│       │   │   │   └── types/
│       │   │   │       └── publicSearch.types.ts
│       │   │   ├── reports/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── reportsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── reportsApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useReports.ts
│       │   │   │   └── types/
│       │   │   │       ├── deptLeaderReports.types.ts
│       │   │   │       ├── reports.types.ts
│       │   │   │       └── staffLeaderReports.types.ts
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
│       │   │       │   │   ├── PartnerOrgCombobox.tsx
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
│       │   │   │   │   ├── AssignedTaskList.tsx
│       │   │   │   │   ├── DeclineReasonModal.tsx
│       │   │   │   │   ├── DeptStaffDashboard.tsx
│       │   │   │   │   ├── ProposalModal.tsx
│       │   │   │   │   ├── StaffCalendarTab.tsx
│       │   │   │   │   ├── StaffLeaderTaskModal.tsx
│       │   │   │   │   ├── StaffTasksTab.tsx
│       │   │   │   │   └── useDeptStaffData.ts
│       │   │   │   ├── departments/
│       │   │   │   │   ├── DepartmentDetailDashboard.tsx
│       │   │   │   │   ├── DepartmentManagement.tsx
│       │   │   │   │   ├── SharedDashboardView.tsx
│       │   │   │   │   ├── TaskDetail.tsx
│       │   │   │   │   ├── TaskHandoverModal.tsx
│       │   │   │   │   └── TaskInvitationDetail.tsx
│       │   │   │   ├── documents/
│       │   │   │   │   ├── DocumentDetailModal.tsx
│       │   │   │   │   └── DocumentManagement.tsx
│       │   │   │   ├── emails/
│       │   │   │   │   ├── CreateEmail.tsx
│       │   │   │   │   ├── EditEmail.tsx
│       │   │   │   │   ├── EmailDetail.tsx
│       │   │   │   │   ├── EmailManagement.tsx
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
│       │   │   │   │   ├── staff-calendar/
│       │   │   │   │   │   ├── StaffDashboardCalendar.tsx
│       │   │   │   │   │   └── StaffVisitDetailModal.tsx
│       │   │   │   │   ├── AdminDashboardView.tsx
│       │   │   │   │   ├── DashboardHome.tsx
│       │   │   │   │   ├── DeptLeadDashboardView.tsx
│       │   │   │   │   └── HODashboardView.tsx
│       │   │   │   ├── minutes/
│       │   │   │   │   ├── MinuteManagement.tsx
│       │   │   │   │   ├── minutesApi.ts
│       │   │   │   │   ├── types.ts
│       │   │   │   │   └── useMinutes.ts
│       │   │   │   ├── news/
│       │   │   │   │   ├── CreateNews.tsx
│       │   │   │   │   ├── EditNews.tsx
│       │   │   │   │   ├── NewsDetailDashboard.tsx
│       │   │   │   │   └── NewsManagement.tsx
│       │   │   │   ├── partners/
│       │   │   │   │   ├── CreatePartner.tsx
│       │   │   │   │   ├── PartnerDetail.tsx
│       │   │   │   │   ├── PartnerEdit.tsx
│       │   │   │   │   └── PartnerManagement.tsx
│       │   │   │   ├── profile/
│       │   │   │   │   └── Profile.tsx
│       │   │   │   ├── reports/
│       │   │   │   │   ├── DeptReportManagement.tsx
│       │   │   │   │   ├── HoReportManagement.tsx
│       │   │   │   │   └── StaffLeaderReportManagement.tsx
│       │   │   │   └── visit/
│       │   │   │       ├── components/
│       │   │   │       │   ├── MediaContributionSection.tsx
│       │   │   │       │   ├── MinutesContributionSection.tsx
│       │   │   │       │   └── NewsContributionSection.tsx
│       │   │   │       ├── AgendaTemplateManagement.tsx
│       │   │   │       ├── CreateVisitRequest.tsx
│       │   │   │       ├── DeptLeadVisitTasksPage.tsx
│       │   │   │       ├── EditVisitRequest.tsx
│       │   │   │       ├── HoVisitProcessDetail.tsx
│       │   │   │       ├── MinutesCard.tsx
│       │   │   │       ├── VisitAfterTab.tsx
│       │   │   │       ├── VisitContributionPage.tsx
│       │   │   │       ├── VisitDuringTab.tsx
│       │   │   │       ├── VisitFeedbackPage.tsx
│       │   │   │       ├── VisitNewsSection.tsx
│       │   │   │       ├── VisitorVisitDetailPage.tsx
│       │   │   │       ├── VisitParticipantInvitationDetail.tsx
│       │   │   │       ├── VisitProcess.tsx
│       │   │   │       ├── VisitProcessSummaryPage.tsx
│       │   │   │       ├── VisitRequestDetail.tsx
│       │   │   │       └── VisitRequestManagement.tsx
│       │   │   ├── public/
│       │   │   │   └── news/
│       │   │   ├── CampusDetailVisitPage.tsx
│       │   │   ├── FAQPage.tsx
│       │   │   ├── ForbiddenPage.tsx
│       │   │   ├── HomePage.tsx
│       │   │   ├── InternalHomePage.tsx
│       │   │   ├── InvalidAccountPage.tsx
│       │   │   ├── NewsDetailPage.tsx
│       │   │   ├── NewsPage.tsx
│       │   │   ├── NotFoundPage.tsx
│       │   │   ├── PartnerDetailPage.tsx
│       │   │   ├── PartnersPage.tsx
│       │   │   ├── PublicHomePage.tsx
│       │   │   └── VisitFPTUPage.tsx
│       │   ├── shared/
│       │   │   ├── api/
│       │   │   │   ├── authInterceptor.ts
│       │   │   │   ├── endpoints.ts
│       │   │   │   ├── errorHandler.ts
│       │   │   │   ├── filesApi.ts
│       │   │   │   ├── fileUploadApi.ts
│       │   │   │   └── httpClient.ts
│       │   │   ├── auth/
│       │   │   │   ├── AuthContext.tsx
│       │   │   │   ├── authStorage.ts
│       │   │   │   ├── dashboardRoute.ts
│       │   │   │   ├── permissionChecker.ts
│       │   │   │   ├── ProtectedRoute.tsx
│       │   │   │   ├── resolveEffectiveRole.ts
│       │   │   │   ├── resolveHomeRoleBucket.ts
│       │   │   │   └── RoleGuard.tsx
│       │   │   ├── constants/
│       │   │   │   ├── appRoutes.ts
│       │   │   │   ├── auth.ts
│       │   │   │   ├── countryCoordinates.ts
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
│       │   │       ├── fileDownload.ts
│       │   │       ├── fileUtils.ts
│       │   │       ├── fileValidation.ts
│       │   │       ├── formatUtils.ts
│       │   │       ├── nameInitials.ts
│       │   │       ├── passwordPolicy.ts
│       │   │       ├── resolveFileUrl.ts
│       │   │       ├── routeUtils.ts
│       │   │       ├── toast.ts
│       │   │       └── validationUtils.ts
│       │   ├── App.tsx
│       │   ├── index.css
│       │   ├── main.tsx
│       │   ├── types.ts
│       │   └── vite-env.d.ts
│       ├── .env
│       ├── .env.example
│       ├── .gitignore
│       ├── index.html
│       ├── metadata.json
│       ├── package-lock.json
│       ├── package.json
│       ├── README.md
│       ├── ts_errors.txt
│       ├── tsconfig.json
│       ├── tsconfig.tsbuildinfo
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
│   │   │   ├── ApproveCampusInstanceCommandTests.cs
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
│   │   │   ├── ProposeResourceModificationCommandTests.cs
│   │   │   ├── RejectCampusInstanceCommandTests.cs
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
│   │   ├── Faqs/
│   │   │   ├── CreateFaq/
│   │   │   │   └── CreateFaqApiTests.cs
│   │   │   ├── UpdateFaq/
│   │   │   │   └── UpdateFaqApiTests.cs
│   │   │   └── ViewListFaq/
│   │   │       └── ViewListFaqApiTests.cs
│   │   ├── Security/
│   │   │   ├── OwnershipCheckerTests.cs
│   │   │   └── PermissionCheckerTests.cs
│   │   ├── TestInfrastructure/
│   │   │   ├── DatabaseResetHelper.cs
│   │   │   ├── PemsWebApplicationFactory.cs
│   │   │   └── TestAuthHandler.cs
│   │   ├── AssemblyInfo.cs
│   │   └── PEMS.IntegrationTests.csproj
│   ├── PEMS.UnitTests/
│   │   ├── Application/
│   │   │   └── ApplicationDummyTest.cs
│   │   ├── Domain/
│   │   │   └── DomainDummyTest.cs
│   │   ├── Faqs/
│   │   │   ├── CreateFaq/
│   │   │   │   └── CreateFaqCommandValidatorTests.cs
│   │   │   ├── UpdateFaq/
│   │   │   │   └── UpdateFaqCommandValidatorTests.cs
│   │   │   └── ViewListFaq/
│   │   │       └── ViewListFaqQueryValidatorTests.cs
│   │   ├── SharedKernel/
│   │   │   └── SharedKernelDummyTest.cs
│   │   └── PEMS.UnitTests.csproj
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

Tóm tắt ngắn từng khu vực:

- backend/PEMS.Api:
  Vai trò API layer, Controllers, Middleware, Filters, Extensions.
- backend/PEMS.Application:
  Vai trò use case layer, CQRS Commands/Queries/Handlers, DTOs, Validators, Interfaces.
- backend/PEMS.Domain:
  Vai trò domain layer, Entities, Enums, Events, ValueObjects, Common base classes.
- backend/PEMS.Infrastructure:
  Vai trò persistence/external services layer, DbContext, Repositories, Identity, Email, FileStorage, Logging.
- frontend/pems-react:
  Vai trò React client, pages/components/services/routes/store nếu có.
- database:
  Vai trò schema, seed, migration, deployment scripts.
- docs:
  Vai trò tài liệu kiến trúc, use cases, permissions, API, database, authentication.

## 4. Important Notes

Ghi lại các lưu ý phát hiện được khi quét:
- Đã cập nhật lại toàn bộ structure từ source thật.
- Các module và file mới đều được ghi nhận.
- Các folder sinh ra trong lúc build và run được loại trừ để tập trung vào mã nguồn.

## 5. Change Summary

- Đã quét lại cấu trúc từ source hiện tại.
- Đã cập nhật tree theo trạng thái thật.
- Đã loại trừ generated folders.
- Không sửa code.
