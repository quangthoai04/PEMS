# Project Structure Detailed Explanation

## 1. Project Overview
- **Project name:** PEMS (FPT Education Visitor Management System)
- **Main purpose:** Quản lý toàn diện quy trình đón tiếp đoàn khách (Delegation Reception Management) và các tài nguyên liên quan tại các cơ sở giáo dục của FPT.
- **Backend:** .NET 9 Web API (Clean Architecture).
- **Frontend:** React với Vite, TypeScript.
- **Database:** MySQL (truy cập qua Entity Framework Core).
- **Documentation:** Nằm tại thư mục `docs/`.
- **Testing:** Nằm tại thư mục `tests/` (Unit Tests, Integration Tests, Application Tests).
- **Tools/scripts:** Database scripts nằm trong `database/scripts/`, frontend scripts nằm trong `frontend/pems-react/scripts/`.

## 2. Full Project Tree

```text
﻿PEMS/
├── .git/ [ignored - version control]
├── .vs/ [ignored - IDE settings]
├── .vscode/ [ignored - IDE settings]
├── backend/
│   ├── PEMS.Api/
│   │   ├── bin/ [ignored - build output]
│   │   ├── Contracts/
│   │   │   ├── ApiResponse.cs
│   │   │   └── ApiRoutes.cs
│   │   ├── Controllers/
│   │   │   ├── Accounts/
│   │   │   │   └── AccountsController.cs
│   │   │   ├── AgendaTemplates/
│   │   │   │   └── AgendaTemplatesController.cs
│   │   │   ├── ApiIntegrations/
│   │   │   │   └── ApiIntegrationsController.cs
│   │   │   ├── Auth/
│   │   │   │   └── AuthController.cs
│   │   │   ├── Calendars/
│   │   │   │   └── CalendarsController.cs
│   │   │   ├── Campuses/
│   │   │   │   └── CampusesController.cs
│   │   │   ├── Delegations/
│   │   │   │   └── DelegationsController.cs
│   │   │   ├── Departments/
│   │   │   │   └── DepartmentsController.cs
│   │   │   ├── Documents/
│   │   │   │   └── DocumentsController.cs
│   │   │   ├── Emails/
│   │   │   │   └── EmailsController.cs
│   │   │   ├── Feedbacks/
│   │   │   │   └── FeedbacksController.cs
│   │   │   ├── Minutes/
│   │   │   │   └── MinutesController.cs
│   │   │   ├── News/
│   │   │   │   └── NewsController.cs
│   │   │   ├── Notifications/
│   │   │   │   └── NotificationsController.cs
│   │   │   ├── Partners/
│   │   │   │   └── PartnersController.cs
│   │   │   ├── Profiles/
│   │   │   │   └── ProfilesController.cs
│   │   │   ├── Public/
│   │   │   │   └── PublicController.cs
│   │   │   ├── Reports/
│   │   │   │   └── ReportsController.cs
│   │   │   └── Roles/
│   │   │       └── RolesController.cs
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
│   │   ├── obj/ [ignored - generated files]
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.json
│   │   ├── PEMS.Api.csproj
│   │   ├── Pems_WebAPI.http
│   │   └── Program.cs
│   ├── PEMS.Application/
│   │   ├── AccountManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateAccountManagementItem/
│   │   │   │       ├── CreateAccountManagementItemCommand.cs
│   │   │   │       ├── CreateAccountManagementItemCommandHandler.cs
│   │   │   │       ├── CreateAccountManagementItemCommandValidator.cs
│   │   │   │       └── CreateAccountManagementItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetAccountManagementList/
│   │   │           ├── GetAccountManagementListDto.cs
│   │   │           ├── GetAccountManagementListQuery.cs
│   │   │           └── GetAccountManagementListQueryHandler.cs
│   │   ├── AgendaTemplates/
│   │   │   ├── Commands/
│   │   │   │   └── CreateAgendaTemplatesItem/
│   │   │   │       ├── CreateAgendaTemplatesItemCommand.cs
│   │   │   │       ├── CreateAgendaTemplatesItemCommandHandler.cs
│   │   │   │       ├── CreateAgendaTemplatesItemCommandValidator.cs
│   │   │   │       └── CreateAgendaTemplatesItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetAgendaTemplatesList/
│   │   │           ├── GetAgendaTemplatesListDto.cs
│   │   │           ├── GetAgendaTemplatesListQuery.cs
│   │   │           └── GetAgendaTemplatesListQueryHandler.cs
│   │   ├── ApiManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateApiManagementItem/
│   │   │   │       ├── CreateApiManagementItemCommand.cs
│   │   │   │       ├── CreateApiManagementItemCommandHandler.cs
│   │   │   │       ├── CreateApiManagementItemCommandValidator.cs
│   │   │   │       └── CreateApiManagementItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetApiManagementList/
│   │   │           ├── GetApiManagementListDto.cs
│   │   │           ├── GetApiManagementListQuery.cs
│   │   │           └── GetApiManagementListQueryHandler.cs
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
│   │   │   ├── Dtos/
│   │   │   │   ├── LoginRequest.cs
│   │   │   │   ├── LoginResponse.cs
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── AuthenticationMappingProfile.cs
│   │   │   ├── Rules/
│   │   │   │   └── README.md
│   │   │   └── Services/
│   │   │       ├── AuthService.cs
│   │   │       └── IAuthService.cs
│   │   ├── bin/ [ignored - build output]
│   │   ├── Calendars/
│   │   │   ├── Commands/
│   │   │   │   └── CreateCalendarsItem/
│   │   │   │       ├── CreateCalendarsItemCommand.cs
│   │   │   │       ├── CreateCalendarsItemCommandHandler.cs
│   │   │   │       ├── CreateCalendarsItemCommandValidator.cs
│   │   │   │       └── CreateCalendarsItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetCalendarsList/
│   │   │           ├── GetCalendarsListDto.cs
│   │   │           ├── GetCalendarsListQuery.cs
│   │   │           └── GetCalendarsListQueryHandler.cs
│   │   ├── CampusManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateCampusManagementItem/
│   │   │   │       ├── CreateCampusManagementItemCommand.cs
│   │   │   │       ├── CreateCampusManagementItemCommandHandler.cs
│   │   │   │       ├── CreateCampusManagementItemCommandValidator.cs
│   │   │   │       └── CreateCampusManagementItemResponse.cs
│   │   │   ├── Queries/
│   │   │   │   └── GetCampusManagementList/
│   │   │   │       ├── GetCampusManagementListDto.cs
│   │   │   │       ├── GetCampusManagementListQuery.cs
│   │   │   │       └── GetCampusManagementListQueryHandler.cs
│   │   │   └── Services/
│   │   │       ├── CampusService.cs
│   │   │       └── ICampusService.cs
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
│   │   │   │   ├── ICurrentUserService.cs
│   │   │   │   ├── IDateTimeService.cs
│   │   │   │   ├── IEmailService.cs
│   │   │   │   ├── IExternalApiClient.cs
│   │   │   │   ├── IFaceRecognitionService.cs
│   │   │   │   ├── IFileStorageService.cs
│   │   │   │   ├── IFileValidationService.cs
│   │   │   │   ├── IIdempotencyService.cs
│   │   │   │   ├── INotificationService.cs
│   │   │   │   ├── IOcrService.cs
│   │   │   │   ├── IOwnershipChecker.cs
│   │   │   │   ├── IPermissionChecker.cs
│   │   │   │   └── IRateLimitService.cs
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
│   │   │   │   ├── ConfirmChangeProposal/
│   │   │   │   │   ├── ConfirmChangeProposalCommand.cs
│   │   │   │   │   ├── ConfirmChangeProposalCommandHandler.cs
│   │   │   │   │   ├── ConfirmChangeProposalCommandValidator.cs
│   │   │   │   │   └── ConfirmChangeProposalResponse.cs
│   │   │   │   ├── ConfirmParticipation/
│   │   │   │   │   ├── ConfirmParticipationCommand.cs
│   │   │   │   │   ├── ConfirmParticipationCommandHandler.cs
│   │   │   │   │   ├── ConfirmParticipationCommandValidator.cs
│   │   │   │   │   └── ConfirmParticipationResponse.cs
│   │   │   │   ├── CreateGuestDelegation/
│   │   │   │   │   ├── CreateGuestDelegationCommand.cs
│   │   │   │   │   ├── CreateGuestDelegationCommandHandler.cs
│   │   │   │   │   ├── CreateGuestDelegationCommandValidator.cs
│   │   │   │   │   └── CreateGuestDelegationResponse.cs
│   │   │   │   ├── CreateNewsArticle/
│   │   │   │   │   ├── CreateNewsArticleCommand.cs
│   │   │   │   │   ├── CreateNewsArticleCommandHandler.cs
│   │   │   │   │   ├── CreateNewsArticleCommandValidator.cs
│   │   │   │   │   └── CreateNewsArticleResponse.cs
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
│   │   │   │   └── ViewGuestDelegationList/
│   │   │   │       ├── ViewGuestDelegationListDto.cs
│   │   │   │       ├── ViewGuestDelegationListQuery.cs
│   │   │   │       └── ViewGuestDelegationListQueryHandler.cs
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── DepartmentManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateDepartmentManagementItem/
│   │   │   │       ├── CreateDepartmentManagementItemCommand.cs
│   │   │   │       ├── CreateDepartmentManagementItemCommandHandler.cs
│   │   │   │       ├── CreateDepartmentManagementItemCommandValidator.cs
│   │   │   │       └── CreateDepartmentManagementItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetDepartmentManagementList/
│   │   │           ├── GetDepartmentManagementListDto.cs
│   │   │           ├── GetDepartmentManagementListQuery.cs
│   │   │           └── GetDepartmentManagementListQueryHandler.cs
│   │   ├── Documents/
│   │   │   ├── Commands/
│   │   │   │   └── CreateDocumentsItem/
│   │   │   │       ├── CreateDocumentsItemCommand.cs
│   │   │   │       ├── CreateDocumentsItemCommandHandler.cs
│   │   │   │       ├── CreateDocumentsItemCommandValidator.cs
│   │   │   │       └── CreateDocumentsItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetDocumentsList/
│   │   │           ├── GetDocumentsListDto.cs
│   │   │           ├── GetDocumentsListQuery.cs
│   │   │           └── GetDocumentsListQueryHandler.cs
│   │   ├── EmailManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateEmailManagementItem/
│   │   │   │       ├── CreateEmailManagementItemCommand.cs
│   │   │   │       ├── CreateEmailManagementItemCommandHandler.cs
│   │   │   │       ├── CreateEmailManagementItemCommandValidator.cs
│   │   │   │       └── CreateEmailManagementItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetEmailManagementList/
│   │   │           ├── GetEmailManagementListDto.cs
│   │   │           ├── GetEmailManagementListQuery.cs
│   │   │           └── GetEmailManagementListQueryHandler.cs
│   │   ├── FaqManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateFaqManagementItem/
│   │   │   │       ├── CreateFaqManagementItemCommand.cs
│   │   │   │       ├── CreateFaqManagementItemCommandHandler.cs
│   │   │   │       ├── CreateFaqManagementItemCommandValidator.cs
│   │   │   │       └── CreateFaqManagementItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetFaqManagementList/
│   │   │           ├── GetFaqManagementListDto.cs
│   │   │           ├── GetFaqManagementListQuery.cs
│   │   │           └── GetFaqManagementListQueryHandler.cs
│   │   ├── Feedbacks/
│   │   │   ├── Commands/
│   │   │   │   └── CreateFeedbacksItem/
│   │   │   │       ├── CreateFeedbacksItemCommand.cs
│   │   │   │       ├── CreateFeedbacksItemCommandHandler.cs
│   │   │   │       ├── CreateFeedbacksItemCommandValidator.cs
│   │   │   │       └── CreateFeedbacksItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetFeedbacksList/
│   │   │           ├── GetFeedbacksListDto.cs
│   │   │           ├── GetFeedbacksListQuery.cs
│   │   │           └── GetFeedbacksListQueryHandler.cs
│   │   ├── GalleryManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateGalleryManagementItem/
│   │   │   │       ├── CreateGalleryManagementItemCommand.cs
│   │   │   │       ├── CreateGalleryManagementItemCommandHandler.cs
│   │   │   │       ├── CreateGalleryManagementItemCommandValidator.cs
│   │   │   │       └── CreateGalleryManagementItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetGalleryManagementList/
│   │   │           ├── GetGalleryManagementListDto.cs
│   │   │           ├── GetGalleryManagementListQuery.cs
│   │   │           └── GetGalleryManagementListQueryHandler.cs
│   │   ├── MeetingMinutes/
│   │   │   ├── Commands/
│   │   │   │   └── CreateMeetingMinutesItem/
│   │   │   │       ├── CreateMeetingMinutesItemCommand.cs
│   │   │   │       ├── CreateMeetingMinutesItemCommandHandler.cs
│   │   │   │       ├── CreateMeetingMinutesItemCommandValidator.cs
│   │   │   │       └── CreateMeetingMinutesItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetMeetingMinutesList/
│   │   │           ├── GetMeetingMinutesListDto.cs
│   │   │           ├── GetMeetingMinutesListQuery.cs
│   │   │           └── GetMeetingMinutesListQueryHandler.cs
│   │   ├── NewsManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateNewsManagementItem/
│   │   │   │       ├── CreateNewsManagementItemCommand.cs
│   │   │   │       ├── CreateNewsManagementItemCommandHandler.cs
│   │   │   │       ├── CreateNewsManagementItemCommandValidator.cs
│   │   │   │       └── CreateNewsManagementItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetNewsManagementList/
│   │   │           ├── GetNewsManagementListDto.cs
│   │   │           ├── GetNewsManagementListQuery.cs
│   │   │           └── GetNewsManagementListQueryHandler.cs
│   │   ├── Notifications/
│   │   │   ├── Commands/
│   │   │   │   └── CreateNotificationsItem/
│   │   │   │       ├── CreateNotificationsItemCommand.cs
│   │   │   │       ├── CreateNotificationsItemCommandHandler.cs
│   │   │   │       ├── CreateNotificationsItemCommandValidator.cs
│   │   │   │       └── CreateNotificationsItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetNotificationsList/
│   │   │           ├── GetNotificationsListDto.cs
│   │   │           ├── GetNotificationsListQuery.cs
│   │   │           └── GetNotificationsListQueryHandler.cs
│   │   ├── obj/ [ignored - generated files]
│   │   ├── Partners/
│   │   │   ├── Commands/
│   │   │   │   ├── CreatePartnerProfile/
│   │   │   │   │   ├── CreatePartnerProfileCommand.cs
│   │   │   │   │   ├── CreatePartnerProfileCommandHandler.cs
│   │   │   │   │   ├── CreatePartnerProfileCommandValidator.cs
│   │   │   │   │   └── CreatePartnerProfileResponse.cs
│   │   │   │   ├── EditPartnerInformation/
│   │   │   │   │   ├── EditPartnerInformationCommand.cs
│   │   │   │   │   ├── EditPartnerInformationCommandHandler.cs
│   │   │   │   │   ├── EditPartnerInformationCommandValidator.cs
│   │   │   │   │   └── EditPartnerInformationResponse.cs
│   │   │   │   ├── ProcessPartnerCreationRequest/
│   │   │   │   │   ├── ProcessPartnerCreationRequestCommand.cs
│   │   │   │   │   ├── ProcessPartnerCreationRequestCommandHandler.cs
│   │   │   │   │   ├── ProcessPartnerCreationRequestCommandValidator.cs
│   │   │   │   │   └── ProcessPartnerCreationRequestResponse.cs
│   │   │   │   └── ScanBusinessCard/
│   │   │   │       ├── ScanBusinessCardCommand.cs
│   │   │   │       ├── ScanBusinessCardCommandHandler.cs
│   │   │   │       ├── ScanBusinessCardCommandValidator.cs
│   │   │   │       └── ScanBusinessCardResponse.cs
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
│   │   │   │   ├── ViewPartners/
│   │   │   │   │   ├── ViewPartnersDto.cs
│   │   │   │   │   ├── ViewPartnersQuery.cs
│   │   │   │   │   └── ViewPartnersQueryHandler.cs
│   │   │   │   └── ViewPolicyTerms/
│   │   │   │       ├── ViewPolicyTermsDto.cs
│   │   │   │       ├── ViewPolicyTermsQuery.cs
│   │   │   │       └── ViewPolicyTermsQueryHandler.cs
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── Reports/
│   │   │   ├── Commands/
│   │   │   │   └── CreateReportsItem/
│   │   │   │       ├── CreateReportsItemCommand.cs
│   │   │   │       ├── CreateReportsItemCommandHandler.cs
│   │   │   │       ├── CreateReportsItemCommandValidator.cs
│   │   │   │       └── CreateReportsItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetReportsList/
│   │   │           ├── GetReportsListDto.cs
│   │   │           ├── GetReportsListQuery.cs
│   │   │           └── GetReportsListQueryHandler.cs
│   │   ├── RolePermissionManagement/
│   │   │   ├── Commands/
│   │   │   │   └── CreateRolePermissionManagementItem/
│   │   │   │       ├── CreateRolePermissionManagementItemCommand.cs
│   │   │   │       ├── CreateRolePermissionManagementItemCommandHandler.cs
│   │   │   │       ├── CreateRolePermissionManagementItemCommandValidator.cs
│   │   │   │       └── CreateRolePermissionManagementItemResponse.cs
│   │   │   └── Queries/
│   │   │       └── GetRolePermissionManagementList/
│   │   │           ├── GetRolePermissionManagementListDto.cs
│   │   │           ├── GetRolePermissionManagementListQuery.cs
│   │   │           └── GetRolePermissionManagementListQueryHandler.cs
│   │   └── PEMS.Application.csproj
│   ├── PEMS.Domain/
│   │   ├── bin/ [ignored - build output]
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
│   │   ├── obj/ [ignored - generated files]
│   │   ├── ValueObjects/
│   │   │   ├── Address.cs
│   │   │   ├── DateRange.cs
│   │   │   ├── EmailAddress.cs
│   │   │   ├── FileMetadata.cs
│   │   │   └── PhoneNumber.cs
│   │   └── PEMS.Domain.csproj
│   ├── PEMS.Infrastructure/
│   │   ├── bin/ [ignored - build output]
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
│   │   │   ├── FileValidationService.cs
│   │   │   ├── LocalFileStorageService.cs
│   │   │   └── VirusScanService.cs
│   │   ├── Idempotency/
│   │   │   └── IdempotencyService.cs
│   │   ├── Identity/
│   │   │   ├── CurrentUserService.cs
│   │   │   ├── JwtTokenService.cs
│   │   │   ├── OwnershipChecker.cs
│   │   │   ├── PasswordHasher.cs
│   │   │   ├── PermissionChecker.cs
│   │   │   └── RefreshTokenStore.cs
│   │   ├── Logging/
│   │   │   ├── ApiRequestLogService.cs
│   │   │   └── AuditLogService.cs
│   │   ├── obj/ [ignored - generated files]
│   │   ├── Persistence/
│   │   │   ├── Configurations/
│   │   │   │   └── UserConfiguration.cs
│   │   │   ├── Migrations/
│   │   │   │   └── MigrationScript.cs
│   │   │   ├── Repositories/
│   │   │   │   ├── DelegationRepository.cs
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
│   │   │   └── RedisRateLimitStore.cs
│   │   ├── DependencyInjection.cs
│   │   └── PEMS.Infrastructure.csproj
│   └── PEMS.SharedKernel/
│       └── README.md
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
│   │   └── API_SPECIFICATION.md
│   ├── architecture/
│   │   ├── CLEAN_ARCHITECTURE.md
│   │   ├── PROJECT_STRUCTURE_DETAILED_EXPLANATION.md
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
│       ├── node_modules/ [ignored - dependency folder]
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
│       │   │   │   ├── hola_new.jpg
│       │   │   │   ├── QuanAP.jpg
│       │   │   │   └── QuyNhon.png
│       │   │   ├── images/
│       │   │   │   ├── 2021-FPTU-Eng.png
│       │   │   │   ├── banner.jpg
│       │   │   │   ├── banner_partner.png
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
│       ├── package-lock.json
│       ├── README.md
│       ├── transform.cjs
│       ├── transform_editable.cjs
│       ├── transform_setup_editable.cjs
│       ├── tsconfig.json
│       ├── updateHeaders.cjs
│       └── vite.config.ts
├── tests/
│   ├── PEMS.ApplicationTests/
│   │   ├── Accounts/
│   │   │   └── CreateAccountCommandHandlerTests.cs
│   │   ├── Delegations/
│   │   │   ├── ProcessVisitRequestCommandHandlerTests.cs
│   │   │   └── SubmitVisitRequestCommandHandlerTests.cs
│   │   ├── Departments/
│   │   │   └── DepartmentTests.cs
│   │   ├── Partners/
│   │   │   └── PartnerTests.cs
│   │   └── Permissions/
│   │       └── ConfigureRolePermissionsCommandHandlerTests.cs
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
├── part1.md
├── part2.md
├── PEMS.slnx
├── README.md
└── tree.txt
```

## 3. Empty Folder Check

Không phát hiện folder trống hoàn toàn (completely empty) trong phạm vi source code chính. Một số folder như `Dtos/` hoặc `Rules/` chỉ chứa file `README.md` để giữ chỗ.

| Empty Folder | Module | Có nên giữ không? | Đề xuất xử lý |
| ------------ | ------ | ----------------- | ------------- |
| (Không có) | N/A | N/A | N/A |

## 4. Main Folder Explanation

| Folder | Vai trò | Chứa gì | Khi nào cần sửa | Lưu ý |
| ------ | ------- | ------- | --------------- | ----- |
| `backend/` | Source code Backend | Chứa toàn bộ các project C# (.NET) | Khi có thay đổi về API, logic, DB | Áp dụng Clean Architecture |
| `backend/PEMS.Api/` | API / Presentation Layer | Controllers, Middleware, Filters, Program.cs | Khi cần thêm route API, filter, middleware mới | Không chứa business logic |
| `backend/PEMS.Application/` | Application / Use Case Layer | Commands, Queries, Handlers, Validators, DTOs | Khi có thay đổi về nghiệp vụ (Use Case) | Phụ thuộc vào Domain, không phụ thuộc Framework |
| `backend/PEMS.Domain/` | Domain / Core Layer | Entities, Enums, ValueObjects, Events | Khi định nghĩa cấu trúc dữ liệu hoặc core rules | Lớp lõi, không phụ thuộc lớp nào khác |
| `backend/PEMS.Infrastructure/` | Infrastructure Layer | DbContext, Email, FileStorage, Identity | Khi đổi DB, cấu hình thư viện ngoài, authentication | Nơi giao tiếp với thế giới thực |
| `backend/PEMS.SharedKernel/` | Shared Utilities | Code dùng chung giữa các layer (nếu có) | Khi cần code dùng được ở nhiều nơi | Hiện tại chỉ chứa README |
| `frontend/` | Source code Frontend | Chứa React project | Khi thay đổi giao diện, flow UI | Tách biệt với backend |
| `frontend/pems-react/` | Root project React | `package.json`, cấu hình Vite, src | Khi update package, thay đổi config build | Chạy lệnh npm tại đây |
| `frontend/pems-react/src/pages/` | Page components | Các màn hình chính và router | Khi thêm màn hình mới | Map 1-1 với route URL |
| `frontend/pems-react/src/components/` | Reusable UI components | Các component UI nhỏ dùng chung (button, modal, layout) | Khi cần update UI system | Tránh chứa logic nghiệp vụ nặng |
| `frontend/pems-react/src/features/` | Feature modules | Logic (api, hooks, types, adapters) theo từng module | Khi thay đổi logic gọi API, xử lý data của module | Tách biệt logic khỏi UI |
| `frontend/pems-react/src/shared/` | Shared Frontend Core | httpClient, auth, utils, constants | Khi đổi cách gọi API, rule phân quyền, utils chung | Hạ tầng của frontend |
| `frontend/pems-react/src/assets/` | Static Assets | Images, Logos, CSS global | Khi thêm/sửa hình ảnh tĩnh | Hình ảnh nặng nên cẩn thận |
| `database/` | Database Management | SQL scripts, migrations, seed data | Khi thiết kế lại DB, thêm dữ liệu mẫu | Không chứa connection string |
| `database/scripts/` | Init Scripts | Script khởi tạo schema DB | Khi có schema mới | Dành cho dev setup môi trường |
| `database/seed/` | Data Seed Scripts | Dữ liệu mẫu ban đầu (admin, campus, roles) | Khi thêm role, campus tĩnh mới | |
| `database/migrations/` | EF Migrations History | Lịch sử chạy migration | Khi chạy tool EF migration | Có thể dùng cho CI/CD |
| `docs/` | System Documentation | Tài liệu API, Architecture, DB, Permissions, Use Cases | Khi thay đổi quy trình, thiết kế hệ thống | Mọi dev/BA/Tester cùng đọc |
| `tests/` | Automated Tests | Unit tests, Integration tests | Khi thêm chức năng cần cover test | Cần update liên tục |
| `frontend/pems-react/scripts/` | Frontend Build Scripts | Script hỗ trợ update, transform component | Khi bảo trì công cụ sinh code | Công cụ nội bộ frontend |

## 5. Backend Structure Analysis

### 5.1 API Layer
- **Controllers nằm ở đâu:** `backend/PEMS.Api/Controllers/`
- **Mỗi nhóm controller đại diện module nào:** Chia theo nghiệp vụ thực tế (Accounts, Delegations, Partners, v.v.).
- **Middleware nằm ở đâu:** `backend/PEMS.Api/Middleware/`
- **Filters/Attributes nằm ở đâu:** `backend/PEMS.Api/Filters/`
- **Program.cs có vai trò gì:** Điểm bắt đầu của app, cấu hình Dependency Injection, Middleware pipeline.
- **API layer có nên chứa business logic không:** Không. API Layer chỉ tiếp nhận request, route sang Application Layer, và trả về response.
- **Trùng lặp Controller:** Không phát hiện controller cũ và mới bị trùng lặp. Cấu trúc controller hiện tại phân nhóm khá sạch sẽ theo module.

### 5.2 Application Layer
- **Application chứa các module nào:** Các module nghiệp vụ được tổ chức theo Use Case như Authentication, Delegations, Partners, và một số thư mục cấu trúc khung (như `AccountManagement`, `CampusManagement`, v.v.).
- **Commands dùng để làm gì:** Thể hiện hành động thay đổi trạng thái hệ thống (Create, Update, Delete).
- **Queries dùng để làm gì:** Thể hiện hành động lấy dữ liệu (Read).
- **Handlers dùng để làm gì:** Thực thi logic tương ứng cho Command hoặc Query.
- **Validators dùng để làm gì:** Kiểm tra tính hợp lệ của dữ liệu đầu vào (FluentValidation).
- **DTO/Response dùng để làm gì:** Object chứa dữ liệu trả về cho API, không lộ Entity thực tế.
- **Rules dùng để làm gì:** Định nghĩa các hằng số, quy tắc nghiệp vụ đặc thù của module.
- **Behaviours:** `ValidationBehaviour`, `AuthorizationBehaviour`, `AuditLogBehaviour` v.v. là pipeline chặn trước/sau xử lý để thực hiện cross-cutting logic tự động mà không cần viết lại ở từng Handler.
- **Module có skeleton chung chung:** Rất nhiều module như `AccountManagement`, `ApiManagement`, `CampusManagement`, `DepartmentManagement` v.v. đang dùng pattern khung rỗng (`CreateXXXItem`, `GetXXXList`). Chúng là các skeleton được gen ra và cần implement chi tiết.

### 5.3 Domain Layer
- **Entities nằm ở đâu:** `backend/PEMS.Domain/Entities/`
- **Vì sao Entity nên nằm ở Domain:** Entity là trung tâm của phần mềm, chứa các object lõi không phụ thuộc vào công nghệ bên ngoài (như ORM hay framework).
- **Enums dùng để làm gì:** Định nghĩa trạng thái hằng số của hệ thống.
- **ValueObjects dùng để làm gì:** Đại diện cho các giá trị không có định danh (như Address, PhoneNumber, DateRange).
- **Events dùng để làm gì:** Kích hoạt các luồng phản ứng bất đồng bộ (Domain Events) khi một hành động xảy ra.
- **BaseEntity / AuditableEntity / SoftDeleteEntity:** Đảm bảo tính đồng nhất cho các bảng, tự động track ai tạo/sửa và khi nào, cũng như cơ chế xóa mềm (IsDeleted).
- **Trùng lặp Entity:** Không phát hiện Entity bị trùng lặp giữa root và folder con vì tất cả đều nằm gọn trong các folder module bên trong `Entities/`.

### 5.4 Infrastructure Layer
- **DbContext nằm ở đâu:** `backend/PEMS.Infrastructure/Persistence/ApplicationDbContext.cs`
- **Persistence dùng để làm gì:** Cấu hình và tương tác trực tiếp với Database qua Entity Framework.
- **Configurations dùng để làm gì:** Mapping chi tiết Entity vào bảng (Fluent API).
- **Repositories dùng để làm gì:** Trừu tượng hóa thao tác với DB.
- **Seed/Migrations dùng để làm gì:** Quản lý dữ liệu mẫu và thay đổi schema DB ở mức hạ tầng.
- **Identity/JWT/PermissionChecker nằm ở đâu:** `backend/PEMS.Infrastructure/Identity/`
- **Các dịch vụ khác (FileStorage, Email, Logging, RateLimiting, Idempotency):** Cung cấp implementation thực tế cho các interface định nghĩa ở Application Layer để giao tiếp với bên ngoài (Cloud, SMTP, Redis).
- **Trùng lặp ApplicationDbContext:** Không phát hiện. Chỉ có một `ApplicationDbContext` tại `Persistence/`.

## 6. Frontend Structure Analysis
- **Frontend root nằm ở đâu:** `frontend/pems-react/`
- **main.tsx dùng để làm gì:** Khởi tạo React App, render Root Component vào DOM.
- **App.tsx dùng để làm gì:** Chứa các global providers, cấu hình routing chính.
- **pages/ dùng để làm gì:** Màn hình và route. Gắn trực tiếp với URL.
- **dashboard pages gồm:** Accounts, APIs, Campus, Departments, Documents, Emails, FAQ, Feedback, Gallery, Home, Minutes, News, Partners, Permissions, Profile, Reports, Visit.
- **components/ dùng để làm gì:** Component dùng chung (UI elements như buttons, tables, layout, modals).
- **features/ dùng để làm gì:** Logic theo nghiệp vụ (api calls, hooks, types, data formatters, adapters) được chia theo domain module, tách rời khỏi trang hiển thị.
- **shared/ dùng để làm gì:** Hạ tầng frontend dùng chung (httpClient, interceptors, utils, permissions, constants).
- **assets/ dùng để làm gì:** Lưu trữ hình ảnh, icon, font, file tĩnh.
- **Trùng lặp pages và features:** Đang được chia đúng vai trò. `pages/` xử lý render UI, `features/` giữ logic state và fetch data, không bị dẫm chân lên nhau.

## 7. Database Structure Analysis
- **database/ dùng để làm gì:** Nơi tập trung toàn bộ tài nguyên liên quan đến Database (SQL thuần).
- **scripts/ dùng để làm gì:** Chứa script tổng khởi tạo Database.
- **seed/ dùng để làm gì:** Chứa script insert dữ liệu ban đầu.
- **migrations/ dùng để làm gì:** Lưu vết hoặc script thay đổi schema theo thời gian (khi không dùng EF Core auto-migration).
- **Kết nối MySQL thật sự nằm ở đâu:** File `backend/PEMS.Api/appsettings.json`.
- **Connection string nên nằm ở đâu:** `appsettings.json` (khi dev) hoặc Environment Variables (khi deploy).
- **Vì sao database folder không phải nơi backend kết nối trực tiếp:** Vì nó chỉ chứa tài liệu & script độc lập. Backend kết nối qua EF Core Provider và cấu hình Connection String trong Web API layer.
- **Trùng lặp SQL script:** Script `pems_full.sql` nằm ở `database/scripts/`. Không có dấu hiệu bị duplicate lộn xộn trong `docs/`.

## 8. Important File Inventory

| File | Path | Type | Purpose | Related Module | Importance | Notes |
| ---- | ---- | ---- | ------- | -------------- | ---------- | ----- |
| `Program.cs` | `backend/PEMS.Api/` | C# | Khởi động, cấu hình App, DI, Middleware | Toàn hệ thống | Critical | |
| `appsettings.json` | `backend/PEMS.Api/` | JSON | Cấu hình DB, JWT, Redis, API Keys | Hạ tầng | Critical | Không commit secret |
| `ApplicationDbContext.cs` | `backend/PEMS.Infrastructure/Persistence/` | C# | EF Core Context giao tiếp DB | Persistence | Critical | |
| `ApplicationDbContextFactory.cs` | `backend/PEMS.Infrastructure/Persistence/` | C# | Design-time DbContext Factory cho tool | Migration | High | |
| `DependencyInjection.cs` | `backend/PEMS.Infrastructure/` | C# | Register services (DI container) | Hạ tầng | Critical | |
| `AuthController.cs` | `backend/PEMS.Api/Controllers/Auth/` | C# | API endpoints Login/Logout/Refresh | Authentication | High | |
| `PermissionChecker.cs` | `backend/PEMS.Infrastructure/Identity/` | C# | Xử lý kiểm tra quyền hạn user | Security | High | |
| `OwnershipChecker.cs` | `backend/PEMS.Infrastructure/Identity/` | C# | Xác thực Object-level authorization | Security | High | |
| `JwtTokenService.cs` | `backend/PEMS.Infrastructure/Identity/` | C# | Sinh JWT token & Refresh token | Security | High | |
| `CurrentUserService.cs` | `backend/PEMS.Infrastructure/Identity/` | C# | Trích xuất thông tin User từ HTTP Context | Security | High | |
| `AuditLogService.cs` | `backend/PEMS.Infrastructure/Logging/` | C# | Ghi log thay đổi dữ liệu của User | Logging | Medium | |
| `IdempotencyService.cs` | `backend/PEMS.Infrastructure/Idempotency/` | C# | Đảm bảo an toàn không double-submit | Security/Infra | High | |
| `RateLimitMiddleware.cs` | `backend/PEMS.Api/Middleware/` | C# | Giới hạn số lượng request (Chống spam) | Security | High | |
| `ExceptionHandlingMiddleware.cs` | `backend/PEMS.Api/Middleware/` | C# | Bat global exceptions, format chuẩn lỗi | Toàn hệ thống | High | |
| `App.tsx` | `frontend/pems-react/src/` | TSX | Root React App, Routes Provider | Frontend App | Critical | |
| `main.tsx` | `frontend/pems-react/src/` | TSX | Entry point gắn React vào index.html | Frontend App | Critical | |
| `Sidebar.tsx` | `frontend/pems-react/src/components/dashboard/` | TSX | Menu điều hướng chính | UI | High | |
| `DashboardLayout.tsx`| `frontend/pems-react/src/components/layout/` | TSX | Layout bao bọc giao diện quản trị | UI | High | |
| `httpClient.ts` | `frontend/pems-react/src/shared/api/` | TS | Cấu hình Axios, Interceptors gọi API | API Layer | Critical | |
| `endpoints.ts` | `frontend/pems-react/src/shared/api/` | TS | Tập trung khai báo danh sách URL API | API Layer | High | |
| `permissionChecker.ts`| `frontend/pems-react/src/shared/auth/` | TS | Hàm kiểm tra quyền trên UI frontend | Security UI | High | |
| `pems_full.sql` | `database/scripts/` | SQL | Script cài đặt DB khởi tạo | DB | High | |
| `PERMISSION_MATRIX.md`| `docs/permissions/` | MD | Bảng ma trận phân quyền hệ thống | Requirement | High | |
| `USE_CASE_LIST.md` | `docs/use-cases/` | MD | Danh sách tổng hợp Use Case dự án | Requirement | High | |
| `README.md` | `[Root]/` | MD | Tài liệu hướng dẫn setup dự án | Documentation | High | |

## 9. Module and Use Case Mapping

| UC Range | Feature Area | Backend Module | Frontend Folder | Permission Related |
| -------- | ------------ | -------------- | --------------- | ------------------ |
| UC-01 → UC-08 | Public/Common | `PublicContent` | - | N/A |
| UC-09 | Notifications | `Notifications` | `features/notifications` | Có |
| UC-10 → UC-13 | Authentication | `Authentication` | `features/authentication` | N/A |
| UC-14 → UC-16 | Profile | `Profiles` | `features/profile` | Tự thân |
| UC-17 → UC-41 | Delegation Reception Management | `Delegations` | `features/delegations` | Có |
| UC-42 → UC-49 | Email Management | `EmailManagement` | `features/emails` | Có |
| UC-50 → UC-54 | Partner Management | `Partners` | `features/partners` | Có |
| UC-55 → UC-56 | Document Management | `Documents` | `features/documents` | Có |
| UC-57 → UC-61 | Gallery Management | `GalleryManagement` | `features/gallery-management` | Có |
| UC-62 → UC-63 | Minutes Management | `MeetingMinutes` | `features/meeting-minutes` | Có |
| UC-64 → UC-68 | FAQ Management | `FaqManagement` | `features/faq-management` | Có |
| UC-69 → UC-71 | Report Management | `Reports` | `features/reports` | Có |
| UC-72 → UC-78 | Calendar Management | `Calendars` | `features/calendars` | Có |
| UC-79 → UC-80 | Feedback Management | `Feedbacks` | `features/feedbacks` | Có |
| UC-81 → UC-87 | Campus Management | `CampusManagement` | `features/campus-management` | Có |
| UC-88 → UC-94 | News Management | `NewsManagement` | `features/news-management` | Có |
| UC-95 → UC-100 | Account Management | `AccountManagement` | `features/account-management` | Có |
| UC-101 → UC-116| Department Management | `DepartmentManagement` | `features/department-management` | Có |
| UC-117 → UC-121| Role & Permission Management | `RolePermissionManagement` | `features/role-permission-management` | Có |
| UC-122 → UC-130| API Management | `ApiManagement` | `features/api-management` | Có |
| UC-131 → UC-135| Agenda Templates Management | `AgendaTemplates` | `features/agenda-templates` | Có |

*Nhận xét:* Cấu trúc hiện tại đã map khá sát giữa `PEMS.Application` (Backend Module) và `features` (Frontend Folder) theo yêu cầu.

## 10. Validation, Security and Anti-Spam Structure Check

| Area | Có trong project chưa? | File/Folder liên quan | Nhận xét | Đề xuất |
| ---- | ---------------------- | --------------------- | -------- | ------- |
| Request validation | Có | `Filters/ValidationFilter.cs` | Tốt | Dùng FluentValidation. |
| Command validator | Có | `PEMS.Application/*/Commands/*Validator.cs` | Tốt | Đang chuẩn hóa theo MediatR pipeline. |
| Global exception handling | Có | `Middleware/ExceptionHandlingMiddleware.cs` | Tốt | Chuẩn hóa API Response. |
| JWT authentication | Có | `Identity/JwtTokenService.cs` | Tốt | |
| Authorization/Permission Matrix | Có | `Identity/PermissionChecker.cs`, `Filters/PermissionAuthorizeAttribute.cs` | Tốt | Đảm bảo Auth check dựa trên Policy. |
| Ownership check | Có | `Identity/OwnershipChecker.cs` | Tốt | Check quyền sửa tài nguyên cá nhân. |
| Rate limiting/chống spam | Có | `RateLimiting/`, `Middleware/RateLimitMiddleware.cs` | Tốt | Dùng RedisRateLimitStore. |
| Idempotency/chống submit trùng | Có | `Idempotency/`, `Filters/IdempotencyFilter.cs` | Tốt | Xử lý Request lặp lại. |
| Audit log | Có | `Logging/AuditLogService.cs` | Tốt | Cần gắn với các entity Auditable. |
| Request log | Có | `Middleware/RequestLoggingMiddleware.cs` | Tốt | Giúp trace bug. |
| API logs | Có | `Logging/ApiRequestLogService.cs` | Tốt | Log API bên thứ 3. |
| File upload validation | Có | `Filters/FileUploadValidationFilter.cs` | Tốt | Chặn file độc hại. |
| CORS | Có | `Extensions/CorsExtensions.cs` | Tốt | Cần config đúng allow origin khi lên prod. |
| Security headers | Có | `Middleware/SecurityHeadersMiddleware.cs` | Tốt | |
| Password hashing | Có | `Identity/PasswordHasher.cs` | Tốt | Dùng BCrypt/PBKDF2. |
| Token/session management | Có | `Identity/RefreshTokenStore.cs` | Tốt | Quản lý vòng đời Refresh token. |
| Soft delete | Có | `Common/SoftDeleteEntity.cs` | Tốt | |
| CreatedAt/UpdatedAt/By tracking | Có | `Common/AuditableEntity.cs` | Tốt | |

## 11. Duplicate and Overlap Check

| Khu vực | Dấu hiệu trùng | Mức độ rủi ro | Đề xuất |
| ------- | -------------- | ------------- | ------- |
| Domain entity | Không phát hiện trùng giữa Entities/ root và con | Thấp | Cấu trúc đang phân quyền module tốt. |
| DbContext | Chỉ có một ApplicationDbContext | Thấp | An toàn. |
| Controller | Không phát hiện controller bị phân tán | Thấp | Được gom gọn tại `Controllers/[Module]/`. |
| Application Service | Skeletons chưa implementation thật sự (`AccountManagement`, `CampusManagement`, v.v.) có dạng chung chung. | Trung bình | Cần thay đổi dần các lớp skeleton DummyDto/DummyRules thành code nghiệp vụ thật. |
| Docs file | Tài liệu phân bố khá tốt trong từng phân hệ | Thấp | |
| SQL Script | `pems_full.sql` nằm ở `scripts/` thay vì trùng vào `docs/` | Thấp | Phân ranh giới rõ ràng. |
| Frontend pages/features | Đã chia đúng vai trò (UI vs Logic) | Thấp | Cần giám sát duy trì quy tắc này. |

## 12. Risks and Suggested Improvements

| Issue | Location | Risk | Suggested Action |
| ----- | -------- | ---- | ---------------- |
| Skeleton Classes chung chung | `backend/PEMS.Application/` | Medium | Rất nhiều module đang chứa class tự sinh `CreateXXXItem`, `GetXXXList` (vd: `CreateAccountManagementItemCommand`). Cần được đổi tên & implement logic chi tiết dựa theo UC Docs. |
| Cấu hình Hardcode (nếu có) | `appsettings.json` / `.env` | Low | Tránh commit mật khẩu thật / API keys lên git. Cần check file `.env.example`. |
| Trạng thái của Tests | `tests/` | Medium | Các dummy tests cần được thay thế bằng tests thật kiểm chứng logic hệ thống, đặc biệt các filter security. |

## 13. Why This Structure Is Organized This Way

### 13.1 Vì sao tách Backend và Frontend?
- Frontend chịu trách nhiệm giao diện (React, Vite), xử lý tương tác thao tác người dùng, hiển thị thông báo.
- Backend (.NET Web API) chịu trách nhiệm nghiệp vụ, bảo mật, xác thực quyền, tương tác database.
- Tách ra giúp không làm rối logic hiển thị UI với logic xử lý dữ liệu phức tạp. Hệ thống cũng có thể mở rộng nhiều Frontend (Mobile App, Web) dùng chung một Backend.

### 13.2 Vì sao Backend chia Clean Architecture?
- `PEMS.Api`: Nơi nhận request từ người dùng, validate đường dẫn và trả response.
- `PEMS.Application`: Nơi chứa nghiệp vụ lõi (Use Case/Business logic).
- `PEMS.Domain`: Nơi định nghĩa các đối tượng (Entity) và những luật (Rules) bất biến nhất của phần mềm.
- `PEMS.Infrastructure`: Nơi cài đặt cụ thể cách ứng dụng kết nối tới Database, Email, File Storage, External Services.
- `PEMS.SharedKernel`: Nơi chứa code công cụ dùng chung toàn hệ thống.
- Luồng hoạt động đi từ ngoài vào trong:
  `Frontend gọi API` → `Controller (PEMS.Api)` → `Handler (PEMS.Application)` → `Entity/Rules (PEMS.Domain)` → `Lưu DB (PEMS.Infrastructure)` → `Trả kết quả`. Lớp Domain ở giữa không phụ thuộc ai, giúp hệ thống không bị "gãy" khi đổi Framework hoặc DB.

### 13.3 Vì sao Application chia theo Use Case?
- Sử dụng pattern CQRS/MediatR, mỗi Use Case có `Command` (thay đổi) hoặc `Query` (đọc) riêng biệt.
- **Developer** dễ dàng định vị lỗi ở đâu (Vd lỗi tạo Partner → Tìm đúng thư mục `CreatePartnerProfile`).
- **Tester/BA** dễ dàng đối chiếu từ requirement sang code.
- Khắc phục nhược điểm "Service gom hàng nghìn dòng code" của kiến trúc cũ (Fat Services).

### 13.4 Vì sao Domain chia theo module?
- Thay vì gom 50-100 Entities vào chung một thư mục gây khó nhìn, Entity được nhóm theo các nghiệp vụ tương tự (`Partners`, `Delegations`, `Users`).
- Trực quan, dễ quản lý và bảo trì.

### 13.5 Vì sao Infrastructure tách Persistence, Identity, FileStorage, Email, Logging?
- Mỗi nhóm có một trách nhiệm duy nhất (Single Responsibility).
- `Persistence` lo kết nối DB. `Identity` lo check quyền, JWT. `FileStorage` lo upload.
- Khi cần thay đổi Cloud Storage (từ LocalDisk sang AWS S3) hay Email Server, chỉ cần sửa ở Infrastructure, các lớp khác không bị ảnh hưởng.
- Tích hợp thêm các lớp bảo vệ hệ thống: `RateLimiting` chống spam, `Idempotency` chống double submit tiền bạc/duyệt.

### 13.6 Vì sao Frontend giữ pages và thêm features?
- `pages` chịu trách nhiệm giữ cấu trúc UI & Routing hiện tại để không làm vỡ giao diện đang có.
- `features` tách logic call API, format data, hooks riêng. Nhờ đó 80% frontend cũ không bị phá vỡ hoàn toàn mà code được dọn dẹp sạch sẽ, module hóa tốt hơn.

### 13.7 Vì sao cần database folder riêng?
- `database/` lưu trữ toàn bộ SQL Schema, Seed Data, Migration Script để quản lý riêng cấu trúc DB.
- Backend không chứa thông tin môi trường thật. Bất cứ dev nào mới vào dự án có thể lấy script tại đây để setup MySQL cục bộ trong 1 phút.

### 13.8 Vì sao cần docs folder riêng?
- Chứa API Spec, Ma trận quyền, Use Cases làm tài liệu dùng chung cho Developer, QA Tester, BA.
- Code và tài liệu đi liền với nhau trong repository giúp tránh tình trạng tài liệu nằm rải rác trên Google Drive / Confluence bị trôi version.

### 13.9 Vì sao cần kiểm tra duplicate?
- AI hoặc dev đôi khi copy-paste code tạo ra 2 Controller hoặc Entity trùng nhau, gây lỗi xung đột route hoặc Entity Framework mapping sai.
- Quét định kỳ giúp nhận diện rủi ro và làm sạch rác hệ thống (Docs lệch version với DB).

### 13.10 Kết luận ý nghĩa cấu trúc
- Kiến trúc chuẩn bị sẵn sàng cho mở rộng dài hạn (Scalability). 
- Mỗi tầng có vai trò rõ rệt, Developer mới vào dễ nắm bắt, Tester dễ đối soát lỗi theo từng Use Case.
- Nâng cao tính bảo mật từ tầng mạng (Rate Limit) cho tới tầng Logic (Ownership/Permission).
- Vẫn giữ nguyên được Front-End React cũ, đảm bảo ổn định tiến độ.
