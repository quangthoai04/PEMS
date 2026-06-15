# PROJECT STRUCTURE FULL REPORT

## 1. Tổng quan dự án

* Tên dự án: PEMS (Education Management System)
* Công nghệ backend: .NET 9, C# 13, Entity Framework Core
* Công nghệ frontend: React 19, TypeScript, Vite
* Database: MySQL 8.0
* Kiến trúc đang dùng: Clean Architecture (Domain, Application, Infrastructure, Api) cho backend. CQRS Pattern.
* Nhận xét nhanh về cấu trúc hiện tại: Hệ thống đã được tổ chức phân lớp rất tốt. Các thư mục rác (như Scaffolder) đã được loại bỏ. Frontend và Backend được tách biệt rõ ràng.

## 2. Cây thư mục đầy đủ

```
project-root/
├── .vscode/
│   ├── launch.json
│   └── tasks.json
├── backend/
│   ├── PEMS.Api/
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
│   │   │   ├── DelegationsController.cs
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
│   │   │   ├── ReportsController.cs
│   │   │   └── RolesController.cs
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
│   │   │   ├── PermissionAuthorizeAttribute.cs
│   │   │   └── ValidationFilter.cs
│   │   ├── Middleware/
│   │   │   ├── CurrentUserMiddleware.cs
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   ├── RateLimitMiddleware.cs
│   │   │   ├── RequestLoggingMiddleware.cs
│   │   │   └── SecurityHeadersMiddleware.cs
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.json
│   │   ├── Pems_WebAPI.http
│   │   ├── PEMS.Api.csproj
│   │   └── Program.cs
│   ├── PEMS.Application/
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
│   │   │   │   └── UpdateAccountRole/
│   │   │   │       ├── UpdateAccountRoleCommand.cs
│   │   │   │       ├── UpdateAccountRoleCommandHandler.cs
│   │   │   │       ├── UpdateAccountRoleCommandValidator.cs
│   │   │   │       └── UpdateAccountRoleResponse.cs
│   │   │   └── Queries/
│   │   │       ├── SearchandFilterAccounts/
│   │   │       │   ├── SearchandFilterAccountsDto.cs
│   │   │       │   ├── SearchandFilterAccountsQuery.cs
│   │   │       │   └── SearchandFilterAccountsQueryHandler.cs
│   │   │       ├── ViewAccountDetails/
│   │   │       │   ├── ViewAccountDetailsDto.cs
│   │   │       │   ├── ViewAccountDetailsQuery.cs
│   │   │       │   └── ViewAccountDetailsQueryHandler.cs
│   │   │       └── ViewAccountList/
│   │   │           ├── ViewAccountListDto.cs
│   │   │           ├── ViewAccountListQuery.cs
│   │   │           └── ViewAccountListQueryHandler.cs
│   │   ├── AgendaTemplates/
│   │   │   ├── Commands/
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
│   │   │   │   └── UpdateAgendaTemplate/
│   │   │   │       ├── UpdateAgendaTemplateCommand.cs
│   │   │   │       ├── UpdateAgendaTemplateCommandHandler.cs
│   │   │   │       ├── UpdateAgendaTemplateCommandValidator.cs
│   │   │   │       └── UpdateAgendaTemplateResponse.cs
│   │   │   └── Queries/
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
│   │   │   │   │   ├── ForgotPasswordCommandValidator.cs
│   │   │   │   │   └── ForgotPasswordResponse.cs
│   │   │   │   ├── LoginViaCredentials/
│   │   │   │   │   ├── LoginViaCredentialsCommand.cs
│   │   │   │   │   ├── LoginViaCredentialsCommandHandler.cs
│   │   │   │   │   ├── LoginViaCredentialsCommandValidator.cs
│   │   │   │   │   └── LoginViaCredentialsResponse.cs
│   │   │   │   ├── LoginViaSso/
│   │   │   │   │   ├── LoginViaSsoCommand.cs
│   │   │   │   │   ├── LoginViaSsoCommandHandler.cs
│   │   │   │   │   ├── LoginViaSsoCommandValidator.cs
│   │   │   │   │   └── LoginViaSsoResponse.cs
│   │   │   │   └── Logout/
│   │   │   │       ├── LogoutCommand.cs
│   │   │   │       ├── LogoutCommandHandler.cs
│   │   │   │       ├── LogoutCommandValidator.cs
│   │   │   │       └── LogoutResponse.cs
│   │   │   ├── Mappings/
│   │   │   │   └── AuthenticationMappingProfile.cs
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
│   │   │   └── Queries/
│   │   │       ├── SearchandFilterCampus/
│   │   │       │   ├── SearchandFilterCampusDto.cs
│   │   │       │   ├── SearchandFilterCampusQuery.cs
│   │   │       │   └── SearchandFilterCampusQueryHandler.cs
│   │   │       ├── ViewCampusDetails/
│   │   │       │   ├── ViewCampusDetailsDto.cs
│   │   │       │   ├── ViewCampusDetailsQuery.cs
│   │   │       │   └── ViewCampusDetailsQueryHandler.cs
│   │   │       └── ViewCampusList/
│   │   │           ├── ViewCampusListDto.cs
│   │   │           ├── ViewCampusListQuery.cs
│   │   │           └── ViewCampusListQueryHandler.cs
│   │   ├── Common/
│   │   │   ├── Behaviours/
│   │   │   │   ├── AuditLogBehaviour.cs
│   │   │   │   ├── AuthorizationBehaviour.cs
│   │   │   │   ├── IdempotencyBehaviour.cs
│   │   │   │   ├── LoggingBehaviour.cs
│   │   │   │   ├── TransactionBehaviour.cs
│   │   │   │   └── ValidationBehaviour.cs
│   │   │   ├── Exceptions/
│   │   │   │   ├── BusinessRuleException.cs
│   │   │   │   ├── ConflictException.cs
│   │   │   │   ├── ForbiddenException.cs
│   │   │   │   ├── NotFoundException.cs
│   │   │   │   └── ValidationException.cs
│   │   │   ├── Interfaces/
│   │   │   │   ├── IApplicationDbContext.cs
│   │   │   │   ├── IAuditLogService.cs
│   │   │   │   ├── ICampusRepository.cs
│   │   │   │   ├── ICurrentUserService.cs
│   │   │   │   ├── IDateTimeService.cs
│   │   │   │   ├── IDelegationRepository.cs
│   │   │   │   ├── IDocumentRepository.cs
│   │   │   │   ├── IEmailService.cs
│   │   │   │   ├── IExternalApiClient.cs
│   │   │   │   ├── IFaceRecognitionService.cs
│   │   │   │   ├── IFileStorageService.cs
│   │   │   │   ├── IFileValidationService.cs
│   │   │   │   ├── IIdempotencyService.cs
│   │   │   │   ├── IJwtTokenService.cs
│   │   │   │   ├── INotificationService.cs
│   │   │   │   ├── IOcrService.cs
│   │   │   │   ├── IOwnershipChecker.cs
│   │   │   │   ├── IPartnerRepository.cs
│   │   │   │   ├── IPasswordHasher.cs
│   │   │   │   ├── IPermissionChecker.cs
│   │   │   │   ├── IRateLimitService.cs
│   │   │   │   └── IUserRepository.cs
│   │   │   ├── Models/
│   │   │   │   ├── ErrorResponse.cs
│   │   │   │   ├── FileUploadResult.cs
│   │   │   │   ├── PagedResult.cs
│   │   │   │   ├── PaginationRequest.cs
│   │   │   │   ├── Result.cs
│   │   │   │   └── ResultOfT.cs
│   │   │   └── Security/
│   │   │       ├── PermissionConstants.cs
│   │   │       ├── PermissionRequirement.cs
│   │   │       └── UseCasePermissionAttribute.cs
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
│   │   │   │   ├── CloseDelegation/
│   │   │   │   │   ├── CloseDelegationCommand.cs
│   │   │   │   │   ├── CloseDelegationCommandHandler.cs
│   │   │   │   │   ├── CloseDelegationCommandValidator.cs
│   │   │   │   │   └── CloseDelegationResponse.cs
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
│   │   │   │   ├── SubmitVisitRequest/
│   │   │   │   │   ├── SubmitVisitRequestCommand.cs
│   │   │   │   │   ├── SubmitVisitRequestCommandHandler.cs
│   │   │   │   │   ├── SubmitVisitRequestCommandValidator.cs
│   │   │   │   │   └── SubmitVisitRequestResponse.cs
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
│   │   │   │   └── UploadVisitPhotos/
│   │   │   │       ├── UploadVisitPhotosCommand.cs
│   │   │   │       ├── UploadVisitPhotosCommandHandler.cs
│   │   │   │       ├── UploadVisitPhotosCommandValidator.cs
│   │   │   │       └── UploadVisitPhotosResponse.cs
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── DelegationsMappingProfile.cs
│   │   │   ├── Queries/
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
│   │   │   │   └── ViewMeetingMinutesDetails/
│   │   │   │       ├── ViewMeetingMinutesDetailsDto.cs
│   │   │   │       ├── ViewMeetingMinutesDetailsQuery.cs
│   │   │   │       └── ViewMeetingMinutesDetailsQueryHandler.cs
│   │   │   └── Rules/
│   │   │       └── README.md
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
│   │   │   │   └── UpdateDepartment/
│   │   │   │       ├── UpdateDepartmentCommand.cs
│   │   │   │       ├── UpdateDepartmentCommandHandler.cs
│   │   │   │       ├── UpdateDepartmentCommandValidator.cs
│   │   │   │       └── UpdateDepartmentResponse.cs
│   │   │   └── Queries/
│   │   │       ├── SearchandFilterDepartments/
│   │   │       │   ├── SearchandFilterDepartmentsDto.cs
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
│   │   │       │   ├── ViewDepartmentListDto.cs
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
│   │   │       ├── ViewEmail/
│   │   │       │   ├── ViewEmailDto.cs
│   │   │       │   ├── ViewEmailQuery.cs
│   │   │       │   └── ViewEmailQueryHandler.cs
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
│   │   │       └── ViewListFAQ/
│   │   │           ├── ViewListFAQDto.cs
│   │   │           ├── ViewListFAQQuery.cs
│   │   │           └── ViewListFAQQueryHandler.cs
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
│   │   │           └── ViewNewsListQueryHandler.cs
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
│   │   │   │   │   ├── ChangePasswordCommand.cs
│   │   │   │   │   ├── ChangePasswordCommandHandler.cs
│   │   │   │   │   ├── ChangePasswordCommandValidator.cs
│   │   │   │   │   └── ChangePasswordResponse.cs
│   │   │   │   └── UpdateProfile/
│   │   │   │       ├── UpdateProfileCommand.cs
│   │   │   │       ├── UpdateProfileCommandHandler.cs
│   │   │   │       ├── UpdateProfileCommandValidator.cs
│   │   │   │       └── UpdateProfileResponse.cs
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── ProfilesMappingProfile.cs
│   │   │   ├── Queries/
│   │   │   │   └── ViewProfile/
│   │   │   │       ├── ViewProfileDto.cs
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
│   │   │   │   │   └── ViewFaqQueryHandler.cs
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
│   │   ├── Roles/
│   │   │   ├── Commands/
│   │   │   │   ├── ConfigureRolePermissions/
│   │   │   │   │   ├── ConfigureRolePermissionsCommand.cs
│   │   │   │   │   ├── ConfigureRolePermissionsCommandHandler.cs
│   │   │   │   │   ├── ConfigureRolePermissionsCommandValidator.cs
│   │   │   │   │   └── ConfigureRolePermissionsResponse.cs
│   │   │   │   ├── CreateNewRole/
│   │   │   │   │   ├── CreateNewRoleCommand.cs
│   │   │   │   │   ├── CreateNewRoleCommandHandler.cs
│   │   │   │   │   ├── CreateNewRoleCommandValidator.cs
│   │   │   │   │   └── CreateNewRoleResponse.cs
│   │   │   │   ├── DisableAndDeleteRole/
│   │   │   │   │   ├── DisableAndDeleteRoleCommand.cs
│   │   │   │   │   ├── DisableAndDeleteRoleCommandHandler.cs
│   │   │   │   │   ├── DisableAndDeleteRoleCommandValidator.cs
│   │   │   │   │   └── DisableAndDeleteRoleResponse.cs
│   │   │   │   └── UpdateRoleDetails/
│   │   │   │       ├── UpdateRoleDetailsCommand.cs
│   │   │   │       ├── UpdateRoleDetailsCommandHandler.cs
│   │   │   │       ├── UpdateRoleDetailsCommandValidator.cs
│   │   │   │       └── UpdateRoleDetailsResponse.cs
│   │   │   └── Queries/
│   │   │       └── ViewRoleList/
│   │   │           ├── ViewRoleListDto.cs
│   │   │           ├── ViewRoleListQuery.cs
│   │   │           └── ViewRoleListQueryHandler.cs
│   │   ├── DependencyInjection.cs
│   │   └── PEMS.Application.csproj
│   ├── PEMS.Domain/
│   │   ├── Common/
│   │   │   ├── AuditableEntity.cs
│   │   │   ├── BaseEntity.cs
│   │   │   ├── DomainEvent.cs
│   │   │   └── SoftDeleteEntity.cs
│   │   ├── Entities/
│   │   │   ├── AgendaTemplates/
│   │   │   │   ├── AgendaTemplate.cs
│   │   │   │   └── AgendaTemplateItem.cs
│   │   │   ├── ApiIntegrations/
│   │   │   │   └── ApiIntegration.cs
│   │   │   ├── Campuses/
│   │   │   │   └── Campus.cs
│   │   │   ├── Delegations/
│   │   │   │   ├── Delegation.cs
│   │   │   │   ├── VisitAgenda.cs
│   │   │   │   ├── VisitDetail.cs
│   │   │   │   ├── VisitParticipant.cs
│   │   │   │   ├── VisitRequest.cs
│   │   │   │   └── VisitStatusLog.cs
│   │   │   ├── Departments/
│   │   │   │   └── Department.cs
│   │   │   ├── Documents/
│   │   │   │   └── Document.cs
│   │   │   ├── Emails/
│   │   │   │   ├── Email.cs
│   │   │   │   ├── EmailTemplate.cs
│   │   │   │   ├── SentEmail.cs
│   │   │   │   └── SentEmailRecipient.cs
│   │   │   ├── Faqs/
│   │   │   │   └── Faq.cs
│   │   │   ├── Feedbacks/
│   │   │   │   ├── Feedback.cs
│   │   │   │   └── FeedbackItem.cs
│   │   │   ├── Galleries/
│   │   │   │   ├── Gallery.cs
│   │   │   │   ├── GalleryImage.cs
│   │   │   │   ├── GalleryLocation.cs
│   │   │   │   └── GalleryLocationImage.cs
│   │   │   ├── Minutes/
│   │   │   │   ├── Actionitem.cs
│   │   │   │   ├── Minute.cs
│   │   │   │   └── MinuteParticipant.cs
│   │   │   ├── News/
│   │   │   │   └── News.cs
│   │   │   ├── Notifications/
│   │   │   │   └── Notification.cs
│   │   │   ├── Partners/
│   │   │   │   ├── Partner.cs
│   │   │   │   ├── Partnercontact.cs
│   │   │   │   ├── Partnerdocument.cs
│   │   │   │   ├── PartnerHistory.cs
│   │   │   │   └── Partnersynclog.cs
│   │   │   ├── Reports/
│   │   │   │   └── Report.cs
│   │   │   ├── Tasks/
│   │   │   │   ├── PemsTask.cs
│   │   │   │   └── TaskAction.cs
│   │   │   └── Users/
│   │   │       ├── AuditLog.cs
│   │   │       ├── LoginLog.cs
│   │   │       ├── Permission.cs
│   │   │       ├── Role.cs
│   │   │       ├── RolePermission.cs
│   │   │       └── User.cs
│   │   ├── Enums/
│   │   │   ├── AccountStatus.cs
│   │   │   ├── ApiIntegrationStatus.cs
│   │   │   ├── CampusStatus.cs
│   │   │   ├── DelegationStatus.cs
│   │   │   ├── DepartmentStatus.cs
│   │   │   ├── FaqVisibilityStatus.cs
│   │   │   ├── NewsStatus.cs
│   │   │   ├── PermissionCode.cs
│   │   │   ├── UserRoleCode.cs
│   │   │   └── VisitRequestStatus.cs
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
│   │   └── PEMS.Domain.csproj
│   ├── PEMS.Infrastructure/
│   │   ├── Email/
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
│   │   │   ├── CloudFileStorageService.cs
│   │   │   ├── FileStorageService.cs
│   │   │   ├── FileValidationService.cs
│   │   │   ├── LocalFileStorageService.cs
│   │   │   └── VirusScanService.cs
│   │   ├── Idempotency/
│   │   │   └── IdempotencyService.cs
│   │   ├── Identity/
│   │   │   ├── CurrentUserService.cs
│   │   │   ├── JwtTokenService.cs
│   │   │   ├── NotificationService.cs
│   │   │   ├── OwnershipChecker.cs
│   │   │   ├── PasswordHasher.cs
│   │   │   ├── PermissionChecker.cs
│   │   │   └── RefreshTokenStore.cs
│   │   ├── Logging/
│   │   │   ├── ApiRequestLogService.cs
│   │   │   └── AuditLogService.cs
│   │   ├── Persistence/
│   │   │   ├── Configurations/
│   │   │   │   └── UserConfiguration.cs
│   │   │   ├── Migrations/
│   │   │   │   └── MigrationScript.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── CampusRepository.cs
│   │   │   │   ├── DelegationRepository.cs
│   │   │   │   ├── DocumentRepository.cs
│   │   │   │   ├── GenericRepository.cs
│   │   │   │   ├── PartnerRepository.cs
│   │   │   │   ├── ReportRepository.cs
│   │   │   │   └── UserRepository.cs
│   │   │   ├── Seed/
│   │   │   │   ├── AdminAccountSeed.cs
│   │   │   │   ├── CampusSeed.cs
│   │   │   │   ├── PermissionMatrixSeed.cs
│   │   │   │   ├── PermissionSeed.cs
│   │   │   │   └── RoleSeed.cs
│   │   │   ├── ApplicationDbContext.cs
│   │   │   └── ApplicationDbContextFactory.cs
│   │   ├── RateLimiting/
│   │   │   ├── InMemoryRateLimitStore.cs
│   │   │   ├── RateLimitService.cs
│   │   │   └── RedisRateLimitStore.cs
│   │   ├── DependencyInjection.cs
│   │   └── PEMS.Infrastructure.csproj
├── database/
│   ├── migrations/
│   │   └── README.md
│   ├── scripts/
│   │   └── pems_full.sql
│   ├── seed/
│   │   ├── campuses.sql
│   │   ├── permission_matrix.sql
│   │   ├── permissions.sql
│   │   └── roles.sql
│   └── README.md
├── docs/
│   ├── api/
│   │   ├── API_ROUTE_CONVENTION.md
│   │   ├── API_SPECIFICATION.md
│   │   └── FRONTEND_BACKEND_CONTRACT_GAP.md
│   ├── architecture/
│   │   ├── ARCHITECTURE_GUARD_TEST_REPORT.md
│   │   ├── BACKEND_SCAFFOLD_CLEANUP_REPORT.md
│   │   ├── BACKEND_SCAFFOLD_REPORT.md
│   │   ├── BACKEND_USE_CASE_CLASS_BLUEPRINT.md
│   │   ├── CLEAN_ARCHITECTURE.md
│   │   ├── PROJECT_STRUCTURE_FULL_DETAILED.md
│   │   └── REFACTOR_CHANGELOG.md
│   ├── database/
│   │   ├── DATABASE_DEPLOYMENT.md
│   │   └── DATABASE_SCHEMA.md
│   ├── permissions/
│   │   ├── PERMISSION_MATRIX.md
│   │   └── PERMISSION_RULES.md
│   ├── use-cases/
│   │   ├── USE_CASE_LIST.md
│   │   └── USE_CASE_NOTES.md
│   ├── PEMS_AI_Refactor_Project_Structure_Prompt.md
│   ├── PROJECT_OVERVIEW.md
│   ├── Technology.md
│   └── VISITOR_MANAGEMENT_SYSTEM.md
├── frontend/
│   └── pems-react/
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
│       │   │   │   ├── LoginModal.tsx
│       │   │   │   ├── SearchPopup.tsx
│       │   │   │   ├── VisitDetailsModal.tsx
│       │   │   │   └── VisitingFormPopup.tsx
│       │   │   └── partners/
│       │   │       └── GlobeComponent.tsx
│       │   ├── features/
│       │   │   ├── account-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── accountManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── accountManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useAccountManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── accountManagement.types.ts
│       │   │   ├── agenda-templates/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── agendaTemplatesAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── agendaTemplatesApi.ts
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
│       │   │   │   │   └── authenticationApi.ts
│       │   │   │   ├── hooks/
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
│       │   │   │   ├── adapters/
│       │   │   │   │   └── campusManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── campusManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useCampusManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── campusManagement.types.ts
│       │   │   ├── delegations/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── delegationsAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── delegationsApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useDelegations.ts
│       │   │   │   └── types/
│       │   │   │       └── delegations.types.ts
│       │   │   ├── department-management/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── departmentManagementAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── departmentManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useDepartmentManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── departmentManagement.types.ts
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
│       │   │   │   │   └── useEmails.ts
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
│       │   │   │   ├── adapters/
│       │   │   │   │   └── profileAdapter.ts
│       │   │   │   ├── api/
│       │   │   │   │   └── profileApi.ts
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
│       │   │   └── role-permission-management/
│       │   │       ├── adapters/
│       │   │       │   └── rolePermissionManagementAdapter.ts
│       │   │       ├── api/
│       │   │       │   └── rolePermissionManagementApi.ts
│       │   │       ├── hooks/
│       │   │       │   └── useRolePermissionManagement.ts
│       │   │       └── types/
│       │   │           └── rolePermissionManagement.types.ts
│       │   ├── pages/
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
│       │   │   │   ├── permissions/
│       │   │   │   │   └── PermissionManagement.tsx
│       │   │   │   ├── profile/
│       │   │   │   │   └── Profile.tsx
│       │   │   │   ├── reports/
│       │   │   │   │   ├── DeptReportManagement.tsx
│       │   │   │   │   ├── mockReportData.ts
│       │   │   │   │   └── ReportManagement.tsx
│       │   │   │   └── visit/
│       │   │   │       ├── AgendaTemplateManagement.tsx
│       │   │   │       ├── CreateVisitRequest.tsx
│       │   │   │       ├── HoVisitProcessDetail.tsx
│       │   │   │       ├── VisitAfterTab.tsx
│       │   │   │       ├── VisitDuringTab.tsx
│       │   │   │       ├── VisitProcess.tsx
│       │   │   │       ├── VisitRequestDetail.tsx
│       │   │   │       └── VisitRequestManagement.tsx
│       │   │   ├── CampusDetailVisitPage.tsx
│       │   │   ├── FAQPage.tsx
│       │   │   ├── HomePage.tsx
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
│       │   │   │   ├── authStorage.ts
│       │   │   │   ├── permissionChecker.ts
│       │   │   │   ├── ProtectedRoute.tsx
│       │   │   │   └── RoleGuard.tsx
│       │   │   ├── constants/
│       │   │   │   ├── appRoutes.ts
│       │   │   │   ├── permissions.ts
│       │   │   │   ├── roles.ts
│       │   │   │   ├── statusCodes.ts
│       │   │   │   └── ucCodes.ts
│       │   │   ├── hooks/
│       │   │   │   ├── useApiError.ts
│       │   │   │   ├── useAuth.ts
│       │   │   │   ├── useDebounce.ts
│       │   │   │   ├── usePagination.ts
│       │   │   │   └── usePermission.ts
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
│       │   │       ├── routeUtils.ts
│       │   │       └── validationUtils.ts
│       │   ├── App.tsx
│       │   ├── index.css
│       │   ├── main.tsx
│       │   ├── types.ts
│       │   └── vite-env.d.ts
│       ├── .env.example
│       ├── .gitignore
│       ├── fix_process.cjs
│       ├── fix_responsive.cjs
│       ├── index.html
│       ├── metadata.json
│       ├── out.txt
│       ├── package.json
│       ├── README.md
│       ├── transform_editable.cjs
│       ├── transform_setup_editable.cjs
│       ├── transform.cjs
│       ├── tsconfig.json
│       ├── updateHeaders.cjs
│       └── vite.config.ts
├── scripts/
│   └── guard-project-structure.ps1
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
│   │   │   ├── PrepareVisitLogisticsCommandTests.cs
│   │   │   ├── ProcessVisitRequestCommandHandlerTests.cs
│   │   │   ├── ProcessVisitRequestCommandTests.cs
│   │   │   ├── ProposeResourceModificationCommandTests.cs
│   │   │   ├── ScanBusinessCardCommandTests.cs
│   │   │   ├── SearchDelegationsQueryTests.cs
│   │   │   ├── SubmitDelegationFeedbackCommandTests.cs
│   │   │   ├── SubmitVisitRequestCommandHandlerTests.cs
│   │   │   ├── SubmitVisitRequestCommandTests.cs
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
│   └── PEMS.UnitTests/
│       ├── Application/
│       │   └── ApplicationDummyTest.cs
│       ├── Domain/
│       │   └── DomainDummyTest.cs
│       └── SharedKernel/
│           └── SharedKernelDummyTest.cs
├── .gitattributes
├── .gitignore
├── PEMS.slnx
├── README.md
└── tree_scan.json

```

## 3. Giải thích ý nghĩa từng tầng / từng folder chính

### backend/

* Mục đích: Chứa toàn bộ source code của hệ thống API.
* Chứa những gì: Các project C# tuân theo Clean Architecture (`PEMS.Api`, `PEMS.Application`, `PEMS.Domain`, `PEMS.Infrastructure`, `PEMS.SharedKernel`).
* Có hợp lý không: Rất hợp lý và đạt chuẩn.
* Có thiếu gì không: Không thiếu, các tầng đã được định nghĩa đầy đủ.

### frontend/

* Mục đích: Chứa mã nguồn của ứng dụng giao diện người dùng.
* Chứa những gì: Mã nguồn React, components, pages, hooks, services, cấu hình vite, package.json.
* Có hợp lý không: Hợp lý.
* Có thiếu gì không: Không.

### database/

* Mục đích: Chứa các tài nguyên liên quan đến cơ sở dữ liệu.
* Chứa những gì: Script tạo bảng, seed data, hoặc file config database (nếu có).
* Có hợp lý không: Hợp lý để quản lý độc lập với source code.
* Có thiếu gì không: Không.

### docs/

* Mục đích: Chứa tài liệu dự án.
* Chứa những gì: Các báo cáo kiến trúc, tài liệu luồng nghiệp vụ (USE_CASE_LIST.md, ARCHITECTURE_GUARD_TEST_REPORT.md).
* Có hợp lý không: Hợp lý, giúp tập trung tài liệu dễ tra cứu.
* Có thiếu gì không: Không.

### tests/

* Mục đích: Chứa các project Unit Test, Integration Test, Architecture Test.
* Chứa những gì: `PEMS.ArchitectureTests` và các project test tương ứng cho backend.
* Có hợp lý không: Rất hợp lý, tách biệt source code chạy thực tế.
* Có thiếu gì không: Hiện tại cấu trúc backend test đã có. Có thể thiếu e2e test cho frontend.

## 4. Danh sách folder trống

| Folder | Trạng thái | Có nên giữ không | Lý do | Đề xuất xử lý |
| ------ | ---------- | ---------------- | ----- | ------------- |
| (Không có) | Sau khi quét không phát hiện folder trống. | N/A | Dự án đã được cleanup kỹ. | N/A |

## 5. Danh sách file cấu hình quan trọng

| File | Vị trí | Mục đích | Nhận xét |
| ---- | ------ | -------- | -------- |
| `PEMS.slnx` | Root | Solution file của .NET, dùng để quản lý toàn bộ các project backend. | Hợp lý, định dạng slnx mới và gọn gàng. |
| `appsettings.json` | `backend/PEMS.Api/` | Cấu hình cho backend API (chuỗi kết nối DB, JWT, Logging). | Đúng vị trí tiêu chuẩn của ASP.NET Core. |
| `Program.cs` | `backend/PEMS.Api/` | Entry point của backend, cấu hình DI container, middlewares. | Chuẩn cấu trúc Minimal Hosting Model. |
| `.gitignore` | Root | Chứa danh sách các file/folder bỏ qua không commit lên Git. | Cấu hình đúng vị trí root. |
| `README.md` | Root | File giới thiệu chung về dự án, cách setup môi trường. | Đúng vị trí chuẩn. |
| `package.json` | `frontend/` (nếu có) | Quản lý thư viện phụ thuộc và scripts cho Frontend. | Chuẩn xác. |
| `vite.config.ts` | `frontend/` (nếu có) | File cấu hình của Vite bundler. | Chuẩn xác. |
| `tsconfig.json` | `frontend/` (nếu có) | File cấu hình TypeScript. | Chuẩn xác. |

## 6. Danh sách file có vẻ bị đặt sai vị trí

| File/Folder | Vị trí hiện tại | Vấn đề | Vị trí đề xuất |
| ----------- | --------------- | ------ | -------------- |
| `tree_scan.json` | Root | File tạm được tạo ra trong quá trình quét cấu trúc. | Nên xóa sau khi hoàn thành báo cáo. |
| `tree_output.txt` | Root | File tạm chứa định dạng cây thư mục. | Nên xóa sau khi hoàn thành báo cáo. |
| `empty_dirs.json` | Root | File tạm dùng để kiểm tra folder trống. | Nên xóa sau khi hoàn thành báo cáo. |
| `clean_structure_utf8.txt` | Root | File text ghi lại kết quả dọn dẹp trước đó. | Nên chuyển vào thư mục `docs/` hoặc xóa. |

## 7. Danh sách file/folder có khả năng bị trùng

| File/Folder | Vị trí 1 | Vị trí 2 | Vì sao nghi bị trùng | Đề xuất xử lý |
| ----------- | -------- | -------- | -------------------- | ------------- |
| (Không phát hiện) | N/A | N/A | Mã nguồn đã được dọn dẹp, không phát hiện trùng lặp module lớn. | N/A |

## 8. Đánh giá cấu trúc theo kiến trúc dự án

| Tầng | Trạng thái | Nhận xét | Cần sửa gì |
| ---- | ---------- | -------- | ---------- |
| **Domain** | Tốt | Chỉ chứa Entities, Value Objects, Enums, Interfaces. Không phụ thuộc vào thư viện ngoài. | Không cần sửa. |
| **Application** | Tốt | Chứa logic nghiệp vụ. Sử dụng CQRS pattern (Commands, Queries) phân chia theo từng Entity rất rõ ràng. | Không cần sửa. |
| **Infrastructure** | Tốt | Nơi implement các interfaces (Repositories, external services), cấu hình Entity Framework. | Không cần sửa. |
| **API** | Tốt | Nơi tiếp nhận HTTP requests, controllers mỏng, delegate công việc cho Application layer. | Không cần sửa. |
| **Frontend** | Tốt | Tách biệt với backend, độc lập phát triển. | Không cần sửa. |
| **Database** | Tốt | Scripts tách biệt. | Không cần sửa. |
| **Tests** | Tốt | Đã áp dụng Architecture Tests để bảo vệ sự toàn vẹn của kiến trúc. | Không cần sửa. |
| **Docs** | Tốt | Được tổ chức tốt, tài liệu rõ ràng. | Không cần sửa. |

## 9. Những phần nên giữ nguyên

* Toàn bộ cấu trúc **Clean Architecture** của Backend.
* Việc chia tách module theo **Feature/CQRS** trong tầng `PEMS.Application`.
* Các rule bảo vệ kiến trúc trong project `PEMS.ArchitectureTests`.
* Hệ thống thư mục phân chia theo vai trò (backend, frontend, docs, database, tests).

## 10. Những phần cần sửa

* **Cần xóa các file tạm thời ở Root**: `tree_scan.json`, `tree_output.txt`, `empty_dirs.json`, `generate_report.js`, `generate_report.py`.
* **Cần di chuyển**: File `clean_structure_utf8.txt` nên được di chuyển vào folder `docs/` nếu muốn lưu trữ lại lịch sử dọn dẹp, hoặc xóa đi nếu không cần thiết.

## 11. Kết luận

* **Cấu trúc hiện tại đã rõ ràng chưa?** Cấu trúc dự án hiện tại cực kỳ rõ ràng, tuân thủ xuất sắc các nguyên lý của Clean Architecture và CQRS. Dự án sẵn sàng cho quá trình scale-up và mở rộng tính năng.
* **Có còn folder trống không?** Không phát hiện thư mục trống nào.
* **Có còn file bị đặt sai chỗ không?** Hầu như không có, ngoại trừ một vài file log/scan sinh ra trong quá trình audit hiện tại.
* **Có còn file/folder bị trùng không?** Không.
* **Có cần tái cấu trúc tiếp không?** Ở thời điểm này là **KHÔNG**. Cấu trúc đã đạt chuẩn "production-ready". Mọi tính năng mới nên được thêm vào tuân theo đúng quy tắc kiến trúc hiện tại đã được define.

