# PEMS Project Structure (Full Tree)

This document contains the complete and un-abbreviated directory tree of the PEMS project, generated automatically from the source code.

## 1. Directory Tree

`	xt
PEMS/
├── .claude/
│   ├── settings.json
│   └── settings.local.json
├── .gitattributes
├── .gitignore
├── backend/
│   ├── PEMS.Api/
│   │   ├── appsettings.json
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
│   │   │   ├── ExceptionHandlingMiddleware.cs
│   │   │   ├── RateLimitMiddleware.cs
│   │   │   ├── RequestLoggingMiddleware.cs
│   │   │   ├── SecurityHeadersMiddleware.cs
│   │   │   └── SessionValidationMiddleware.cs
│   │   ├── PEMS.Api.csproj
│   │   ├── Pems_WebAPI.http
│   │   ├── Program.cs
│   │   ├── Properties/
│   │   │   ├── launchSettings.json
│   │   ├── test.cs
│   │   └── TestBcrypt/
│   │       ├── Program.cs
│   │       └── TestBcrypt.csproj
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
│   │   │   │   ├── ChangePassword/
│   │   │   │   │   ├── ChangePasswordCommand.cs
│   │   │   │   │   ├── ChangePasswordCommandHandler.cs
│   │   │   │   │   └── ChangePasswordCommandValidator.cs
│   │   │   │   ├── ForgotPassword/
│   │   │   │   │   ├── ForgotPasswordCommand.cs
│   │   │   │   │   ├── ForgotPasswordCommandHandler.cs
│   │   │   │   │   └── ForgotPasswordCommandValidator.cs
│   │   │   │   ├── LoginViaCredentials/
│   │   │   │   │   ├── LoginViaCredentialsCommand.cs
│   │   │   │   │   ├── LoginViaCredentialsCommandHandler.cs
│   │   │   │   │   └── LoginViaCredentialsCommandValidator.cs
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
│   │   │   │   ├── AuthenticationMappingProfile.cs
│   │   │   ├── Models/
│   │   │   │   ├── AuthResponse.cs
│   │   │   │   ├── AuthUserDto.cs
│   │   │   │   ├── MessageResponse.cs
│   │   │   │   ├── PermissionsResponse.cs
│   │   │   │   ├── UserPermissionDto.cs
│   │   │   │   └── UserProfileResponse.cs
│   │   │   ├── Queries/
│   │   │   │   ├── GetCurrentUser/
│   │   │   │   │   ├── GetCurrentUserQuery.cs
│   │   │   │   │   └── GetCurrentUserQueryHandler.cs
│   │   │   │   └── GetCurrentUserPermissions/
│   │   │   │       ├── GetCurrentUserPermissionsQuery.cs
│   │   │   │       └── GetCurrentUserPermissionsQueryHandler.cs
│   │   │   └── Rules/
│   │   │       ├── README.md
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
│   │   │   │   ├── AuthenticationFailedException.cs
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
│   │   │   │   ├── IGoogleTokenValidator.cs
│   │   │   │   ├── IIdempotencyService.cs
│   │   │   │   ├── IJwtTokenService.cs
│   │   │   │   ├── INotificationService.cs
│   │   │   │   ├── IOcrService.cs
│   │   │   │   ├── IOtpService.cs
│   │   │   │   ├── IOwnershipChecker.cs
│   │   │   │   ├── IPartnerRepository.cs
│   │   │   │   ├── IPasswordHasher.cs
│   │   │   │   ├── IPermissionChecker.cs
│   │   │   │   ├── IRateLimitService.cs
│   │   │   │   ├── ISecurityAuditService.cs
│   │   │   │   ├── ISessionService.cs
│   │   │   │   └── IUserRepository.cs
│   │   │   ├── Models/
│   │   │   │   ├── ErrorResponse.cs
│   │   │   │   ├── FileUploadResult.cs
│   │   │   │   ├── PagedResult.cs
│   │   │   │   ├── PaginationRequest.cs
│   │   │   │   ├── Result.cs
│   │   │   │   └── ResultOfT.cs
│   │   │   └── Security/
│   │   │       ├── PasswordPolicy.cs
│   │   │       ├── PemsClaimTypes.cs
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
│   │   │   │   ├── README.md
│   │   │   ├── Mappings/
│   │   │   │   ├── DelegationsMappingProfile.cs
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
│   │   │       ├── README.md
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
│   │   ├── DependencyInjection.cs
│   │   ├── Documents/
│   │   │   ├── Queries/
│   │   │   │   ├── SearchDocuments/
│   │   │   │   │   ├── SearchDocumentsDto.cs
│   │   │   │   │   ├── SearchDocumentsQuery.cs
│   │   │   │   │   └── SearchDocumentsQueryHandler.cs
│   │   │   │   └── ViewDocumentList/
│   │   │   │       ├── ViewDocumentListDto.cs
│   │   │   │       ├── ViewDocumentListQuery.cs
│   │   │   │       └── ViewDocumentListQueryHandler.cs
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
│   │   │   ├── Queries/
│   │   │   │   ├── SearchAndFilterFeedback/
│   │   │   │   │   ├── SearchAndFilterFeedbackDto.cs
│   │   │   │   │   ├── SearchAndFilterFeedbackQuery.cs
│   │   │   │   │   └── SearchAndFilterFeedbackQueryHandler.cs
│   │   │   │   └── ViewFeedbackSummary/
│   │   │   │       ├── ViewFeedbackSummaryDto.cs
│   │   │   │       ├── ViewFeedbackSummaryQuery.cs
│   │   │   │       └── ViewFeedbackSummaryQueryHandler.cs
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
│   │   │   ├── Queries/
│   │   │   │   ├── SearchAndFilterMinutes/
│   │   │   │   │   ├── SearchAndFilterMinutesDto.cs
│   │   │   │   │   ├── SearchAndFilterMinutesQuery.cs
│   │   │   │   │   └── SearchAndFilterMinutesQueryHandler.cs
│   │   │   │   └── ViewMinutesList/
│   │   │   │       ├── ViewMinutesListDto.cs
│   │   │   │       ├── ViewMinutesListQuery.cs
│   │   │   │       └── ViewMinutesListQueryHandler.cs
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
│   │   │   │   ├── README.md
│   │   │   ├── Mappings/
│   │   │   │   ├── PartnersMappingProfile.cs
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
│   │   │       ├── README.md
│   │   ├── PEMS.Application.csproj
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
│   │   │   │   ├── README.md
│   │   │   ├── Mappings/
│   │   │   │   ├── ProfilesMappingProfile.cs
│   │   │   ├── Queries/
│   │   │   │   ├── ViewProfile/
│   │   │   │   │   ├── ViewProfileDto.cs
│   │   │   │   │   ├── ViewProfileQuery.cs
│   │   │   │   │   └── ViewProfileQueryHandler.cs
│   │   │   └── Rules/
│   │   │       ├── README.md
│   │   ├── PublicContent/
│   │   │   ├── Dtos/
│   │   │   │   ├── README.md
│   │   │   ├── Mappings/
│   │   │   │   ├── PublicContentMappingProfile.cs
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
│   │   │       ├── README.md
│   │   ├── Reports/
│   │   │   ├── Commands/
│   │   │   │   ├── ExportStatisticsReport/
│   │   │   │   │   ├── ExportStatisticsReportCommand.cs
│   │   │   │   │   ├── ExportStatisticsReportCommandHandler.cs
│   │   │   │   │   ├── ExportStatisticsReportCommandValidator.cs
│   │   │   │   │   └── ExportStatisticsReportResponse.cs
│   │   │   └── Queries/
│   │   │       ├── FilterDashboardByTime/
│   │   │       │   ├── FilterDashboardByTimeDto.cs
│   │   │       │   ├── FilterDashboardByTimeQuery.cs
│   │   │       │   └── FilterDashboardByTimeQueryHandler.cs
│   │   │       └── ViewDashboardStatistics/
│   │   │           ├── ViewDashboardStatisticsDto.cs
│   │   │           ├── ViewDashboardStatisticsQuery.cs
│   │   │           └── ViewDashboardStatisticsQueryHandler.cs
│   │   └── Roles/
│   │       ├── Commands/
│   │       │   ├── ConfigureRolePermissions/
│   │       │   │   ├── ConfigureRolePermissionsCommand.cs
│   │       │   │   ├── ConfigureRolePermissionsCommandHandler.cs
│   │       │   │   ├── ConfigureRolePermissionsCommandValidator.cs
│   │       │   │   └── ConfigureRolePermissionsResponse.cs
│   │       │   ├── CreateNewRole/
│   │       │   │   ├── CreateNewRoleCommand.cs
│   │       │   │   ├── CreateNewRoleCommandHandler.cs
│   │       │   │   ├── CreateNewRoleCommandValidator.cs
│   │       │   │   └── CreateNewRoleResponse.cs
│   │       │   ├── DisableAndDeleteRole/
│   │       │   │   ├── DisableAndDeleteRoleCommand.cs
│   │       │   │   ├── DisableAndDeleteRoleCommandHandler.cs
│   │       │   │   ├── DisableAndDeleteRoleCommandValidator.cs
│   │       │   │   └── DisableAndDeleteRoleResponse.cs
│   │       │   └── UpdateRoleDetails/
│   │       │       ├── UpdateRoleDetailsCommand.cs
│   │       │       ├── UpdateRoleDetailsCommandHandler.cs
│   │       │       ├── UpdateRoleDetailsCommandValidator.cs
│   │       │       └── UpdateRoleDetailsResponse.cs
│   │       └── Queries/
│   │           ├── ViewRoleList/
│   │           │   ├── ViewRoleListDto.cs
│   │           │   ├── ViewRoleListQuery.cs
│   │           │   └── ViewRoleListQueryHandler.cs
│   ├── PEMS.Domain/
│   │   ├── Common/
│   │   │   ├── AuditableEntity.cs
│   │   │   ├── BaseEntity.cs
│   │   │   ├── DomainEvent.cs
│   │   │   └── SoftDeleteEntity.cs
│   │   ├── Constants/
│   │   │   ├── AuthConstants.cs
│   │   ├── Entities/
│   │   │   ├── AgendaTemplates/
│   │   │   │   ├── AgendaTemplate.cs
│   │   │   ├── ApiIntegrations/
│   │   │   │   ├── ApiConfiguration.cs
│   │   │   │   ├── ApiRequestLog.cs
│   │   │   │   └── ApiUsageQuota.cs
│   │   │   ├── Calendar/
│   │   │   │   ├── CalendarEvent.cs
│   │   │   ├── Campuses/
│   │   │   │   ├── Campus.cs
│   │   │   ├── Delegations/
│   │   │   │   ├── VisitAgenda.cs
│   │   │   │   ├── VisitGuestMember.cs
│   │   │   │   ├── VisitLogisticsItem.cs
│   │   │   │   ├── VisitParticipant.cs
│   │   │   │   ├── VisitRequest.cs
│   │   │   │   ├── VisitRequestCampus.cs
│   │   │   │   └── VisitStatusLog.cs
│   │   │   ├── Departments/
│   │   │   │   ├── Department.cs
│   │   │   ├── Documents/
│   │   │   │   ├── Document.cs
│   │   │   │   └── UploadedFile.cs
│   │   │   ├── Emails/
│   │   │   │   ├── EmailTemplate.cs
│   │   │   │   └── SentEmail.cs
│   │   │   ├── Faqs/
│   │   │   │   ├── Faq.cs
│   │   │   ├── Feedbacks/
│   │   │   │   ├── Feedback.cs
│   │   │   ├── Galleries/
│   │   │   │   ├── Gallery.cs
│   │   │   │   ├── GalleryImage.cs
│   │   │   │   └── PhotoFaceTag.cs
│   │   │   ├── Minutes/
│   │   │   │   ├── Minute.cs
│   │   │   ├── News/
│   │   │   │   ├── News.cs
│   │   │   │   └── NewsTranslation.cs
│   │   │   ├── Notifications/
│   │   │   │   ├── Notification.cs
│   │   │   ├── Partners/
│   │   │   │   ├── Partner.cs
│   │   │   │   └── Partnercontact.cs
│   │   │   ├── PublicContents/
│   │   │   │   ├── PublicContent.cs
│   │   │   └── Users/
│   │   │       ├── AuditLog.cs
│   │   │       ├── LoginLog.cs
│   │   │       ├── OtpToken.cs
│   │   │       ├── Permission.cs
│   │   │       ├── Role.cs
│   │   │       ├── RolePermission.cs
│   │   │       ├── SecurityEvent.cs
│   │   │       ├── User.cs
│   │   │       ├── UserAuthProvider.cs
│   │   │       └── UserSession.cs
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
│   │   ├── PEMS.Domain.csproj
│   │   └── ValueObjects/
│   │       ├── Address.cs
│   │       ├── DateRange.cs
│   │       ├── EmailAddress.cs
│   │       ├── FileMetadata.cs
│   │       └── PhoneNumber.cs
│   └── PEMS.Infrastructure/
│       ├── Common/
│       │   ├── DateTimeService.cs
│       ├── DependencyInjection.cs
│       ├── Email/
│       │   ├── EmailService.cs
│       │   ├── EmailTemplateRenderer.cs
│       │   └── SmtpEmailSender.cs
│       ├── ExternalServices/
│       │   ├── ApiClient/
│       │   │   ├── ExternalApiClient.cs
│       │   ├── Calendar/
│       │   │   ├── CalendarIntegrationService.cs
│       │   ├── FaceRecognition/
│       │   │   ├── FaceRecognitionService.cs
│       │   └── Ocr/
│       │       ├── OcrService.cs
│       ├── FileStorage/
│       │   ├── CloudFileStorageService.cs
│       │   ├── FileStorageService.cs
│       │   ├── FileValidationService.cs
│       │   ├── LocalFileStorageService.cs
│       │   └── VirusScanService.cs
│       ├── Idempotency/
│       │   ├── IdempotencyService.cs
│       ├── Identity/
│       │   ├── CurrentUserService.cs
│       │   ├── GoogleTokenValidator.cs
│       │   ├── JwtTokenService.cs
│       │   ├── NotificationService.cs
│       │   ├── OtpService.cs
│       │   ├── OwnershipChecker.cs
│       │   ├── PasswordHasher.cs
│       │   ├── PermissionChecker.cs
│       │   ├── RefreshTokenStore.cs
│       │   ├── SecureTokenGenerator.cs
│       │   └── SessionService.cs
│       ├── Logging/
│       │   ├── ApiRequestLogService.cs
│       │   ├── AuditLogService.cs
│       │   └── SecurityAuditService.cs
│       ├── PEMS.Infrastructure.csproj
│       ├── Persistence/
│       │   ├── ApplicationDbContext.cs
│       │   ├── ApplicationDbContextFactory.cs
│       │   ├── Configurations/
│       │   │   ├── UserConfiguration.cs
│       │   ├── Repositories/
│       │   │   ├── CampusRepository.cs
│       │   │   ├── DelegationRepository.cs
│       │   │   ├── DocumentRepository.cs
│       │   │   ├── GenericRepository.cs
│       │   │   ├── PartnerRepository.cs
│       │   │   ├── ReportRepository.cs
│       │   │   └── UserRepository.cs
│       │   └── Seed/
│       └── RateLimiting/
│           ├── InMemoryRateLimitStore.cs
│           ├── RateLimitService.cs
│           └── RedisRateLimitStore.cs
├── database/
│   ├── migrations/
│   │   ├── README.md
│   ├── README.md
│   ├── scripts/
│   │   ├── DbSeeder/
│   │   │   ├── DbSeeder.csproj
│   │   │   └── Program.cs
│   │   ├── pems_full.sql
│   │   └── SEED_DATA_CONVENTION.md
│   └── seed/
│       ├── campuses.sql
│       ├── permissions.sql
│       └── roles.sql
├── docs/
│   ├── api/
│   │   ├── API_ROUTE_CONVENTION.md
│   │   ├── API_SPECIFICATION.md
│   │   └── FRONTEND_BACKEND_CONTRACT_GAP.md
│   ├── architecture/
│   │   ├── CLEAN_ARCHITECTURE.md
│   │   └── PROJECT_STRUCTURE_FULL.md
│   ├── authentication/
│   │   ├── AUTHENTICATION_FLOW_REPORT.md
│   ├── database/
│   │   ├── DATABASE_DEPLOYMENT.md
│   │   └── DATABASE_SCHEMA.md
│   ├── PEMS_AI_Refactor_Project_Structure_Prompt.md
│   ├── permissions/
│   │   ├── PERMISSION_MATRIX.md
│   │   └── PERMISSION_RULES.md
│   ├── PROJECT_OVERVIEW.md
│   ├── Technology.md
│   ├── use-cases/
│   │   ├── USE_CASE_LIST.md
│   │   └── USE_CASE_NOTES.md
│   └── VISITOR_MANAGEMENT_SYSTEM.md
├── frontend/
│   ├── pems-react/
│   │   ├── .env
│   │   ├── .env.example
│   │   ├── .gitignore
│   │   ├── fix_process.cjs
│   │   ├── fix_responsive.cjs
│   │   ├── index.html
│   │   ├── metadata.json
│   │   ├── out.txt
│   │   ├── package.json
│   │   ├── package-lock.json
│   │   ├── README.md
│   │   ├── scripts/
│   │   │   ├── applet_update.js
│   │   │   ├── applet_update_contact.js
│   │   │   ├── applet_update_emerald.js
│   │   │   ├── applet_update_visit_3.js
│   │   │   ├── applet_update_visit_4.js
│   │   │   ├── applet_update_vp.js
│   │   │   ├── transform.js
│   │   │   ├── update_ho.js
│   │   │   ├── update_linter.js
│   │   │   ├── update_visit_2.js
│   │   │   ├── update_visit_3.js
│   │   │   ├── update_visit_4.js
│   │   │   └── update_vp.js
│   │   ├── src/
│   │   │   ├── App.tsx
│   │   │   ├── assets/
│   │   │   │   ├── Avatar/
│   │   │   │   │   ├── AvatarDefault.png
│   │   │   │   ├── FPTbanner_visit/
│   │   │   │   │   ├── (8 media files)
│   │   │   │   ├── images/
│   │   │   │   │   ├── (7 media files)
│   │   │   │   ├── img_visit_detail/
│   │   │   │   │   ├── (20 media files)
│   │   │   │   └── Logo/
│   │   │   │       ├── (18 media files)
│   │   │   ├── components/
│   │   │   │   ├── dashboard/
│   │   │   │   │   ├── NotificationBell.tsx
│   │   │   │   │   └── Sidebar.tsx
│   │   │   │   ├── home/
│   │   │   │   │   ├── CTASection.tsx
│   │   │   │   │   ├── HeroSection.tsx
│   │   │   │   │   ├── NewsSection.tsx
│   │   │   │   │   ├── PartnersSection.tsx
│   │   │   │   │   └── StatsSection.tsx
│   │   │   │   ├── layout/
│   │   │   │   │   ├── DashboardLayout.tsx
│   │   │   │   │   ├── Footer.tsx
│   │   │   │   │   └── Header.tsx
│   │   │   │   ├── modals/
│   │   │   │   │   ├── LoginModal.tsx
│   │   │   │   │   ├── SearchPopup.tsx
│   │   │   │   │   ├── VisitDetailsModal.tsx
│   │   │   │   │   └── VisitingFormPopup.tsx
│   │   │   │   └── partners/
│   │   │   │       ├── GlobeComponent.tsx
│   │   │   ├── features/
│   │   │   │   ├── account-management/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── accountManagementAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── accountManagementApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useAccountManagement.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── accountManagement.types.ts
│   │   │   │   ├── agenda-templates/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── agendaTemplatesAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── agendaTemplatesApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useAgendaTemplates.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── agendaTemplates.types.ts
│   │   │   │   ├── api-management/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── apiManagementAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── apiManagementApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useApiManagement.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── apiManagement.types.ts
│   │   │   │   ├── authentication/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── authenticationAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── authenticationApi.ts
│   │   │   │   │   │   └── authError.ts
│   │   │   │   │   ├── components/
│   │   │   │   │   │   ├── DualPortalLoginForms.tsx
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useAuthentication.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── authentication.types.ts
│   │   │   │   ├── calendars/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── calendarsAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── calendarsApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useCalendars.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── calendars.types.ts
│   │   │   │   ├── campus-management/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── campusManagementAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── campusManagementApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useCampusManagement.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── campusManagement.types.ts
│   │   │   │   ├── delegations/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── delegationsAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── delegationsApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useDelegations.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── delegations.types.ts
│   │   │   │   ├── department-management/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── departmentManagementAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── departmentManagementApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useDepartmentManagement.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── departmentManagement.types.ts
│   │   │   │   ├── documents/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── documentsAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── documentsApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useDocuments.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── documents.types.ts
│   │   │   │   ├── emails/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── emailsAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── emailsApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useEmails.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── emails.types.ts
│   │   │   │   ├── faq-management/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── faqManagementAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── faqManagementApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useFaqManagement.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── faqManagement.types.ts
│   │   │   │   ├── feedbacks/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── feedbacksAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── feedbacksApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useFeedbacks.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── feedbacks.types.ts
│   │   │   │   ├── gallery-management/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── galleryManagementAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── galleryManagementApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useGalleryManagement.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── galleryManagement.types.ts
│   │   │   │   ├── meeting-minutes/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── meetingMinutesAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── meetingMinutesApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useMeetingMinutes.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── meetingMinutes.types.ts
│   │   │   │   ├── news-management/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── newsManagementAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── newsManagementApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useNewsManagement.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── newsManagement.types.ts
│   │   │   │   ├── notifications/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── notificationsAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── notificationsApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useNotifications.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── notifications.types.ts
│   │   │   │   ├── partners/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── partnersAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── partnersApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── usePartners.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── partners.types.ts
│   │   │   │   ├── profile/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── profileAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── profileApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useProfile.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── profile.types.ts
│   │   │   │   ├── public-content/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── publicContentAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── publicContentApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── usePublicContent.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── publicContent.types.ts
│   │   │   │   ├── reports/
│   │   │   │   │   ├── adapters/
│   │   │   │   │   │   ├── reportsAdapter.ts
│   │   │   │   │   ├── api/
│   │   │   │   │   │   ├── reportsApi.ts
│   │   │   │   │   ├── hooks/
│   │   │   │   │   │   ├── useReports.ts
│   │   │   │   │   └── types/
│   │   │   │   │       ├── reports.types.ts
│   │   │   │   └── role-permission-management/
│   │   │   │       ├── adapters/
│   │   │   │       │   ├── rolePermissionManagementAdapter.ts
│   │   │   │       ├── api/
│   │   │   │       │   ├── rolePermissionManagementApi.ts
│   │   │   │       ├── hooks/
│   │   │   │       │   ├── useRolePermissionManagement.ts
│   │   │   │       └── types/
│   │   │   │           ├── rolePermissionManagement.types.ts
│   │   │   ├── index.css
│   │   │   ├── main.tsx
│   │   │   ├── pages/
│   │   │   │   ├── auth/
│   │   │   │   │   ├── ChangePasswordPage.tsx
│   │   │   │   │   ├── ForgotPasswordPage.tsx
│   │   │   │   │   ├── LoginPage.tsx
│   │   │   │   │   └── ResetPasswordPage.tsx
│   │   │   │   ├── CampusDetailVisitPage.tsx
│   │   │   │   ├── dashboard/
│   │   │   │   │   ├── accounts/
│   │   │   │   │   │   ├── AccountManagement.tsx
│   │   │   │   │   ├── apis/
│   │   │   │   │   │   ├── ApiManagement.tsx
│   │   │   │   │   ├── campus/
│   │   │   │   │   │   ├── CampusDetail.tsx
│   │   │   │   │   │   └── CampusManagement.tsx
│   │   │   │   │   ├── departments/
│   │   │   │   │   │   ├── DepartmentDetailDashboard.tsx
│   │   │   │   │   │   ├── DepartmentManagement.tsx
│   │   │   │   │   │   ├── TaskDetail.tsx
│   │   │   │   │   │   └── TaskInvitationDetail.tsx
│   │   │   │   │   ├── documents/
│   │   │   │   │   │   ├── DocumentManagement.tsx
│   │   │   │   │   ├── emails/
│   │   │   │   │   │   ├── CreateEmail.tsx
│   │   │   │   │   │   ├── EditEmail.tsx
│   │   │   │   │   │   ├── EmailDetail.tsx
│   │   │   │   │   │   ├── EmailManagement.tsx
│   │   │   │   │   │   ├── SendEmailTab.tsx
│   │   │   │   │   │   └── SentEmailDetail.tsx
│   │   │   │   │   ├── faq/
│   │   │   │   │   │   ├── FAQDetail.tsx
│   │   │   │   │   │   └── FAQManagement.tsx
│   │   │   │   │   ├── feedback/
│   │   │   │   │   │   ├── FeedbackDetail.tsx
│   │   │   │   │   │   ├── FeedbackManagement.tsx
│   │   │   │   │   │   └── mockData.ts
│   │   │   │   │   ├── gallery/
│   │   │   │   │   │   ├── GalleryManagement.tsx
│   │   │   │   │   │   └── LocationManagement.tsx
│   │   │   │   │   ├── home/
│   │   │   │   │   │   ├── AdminDashboardView.tsx
│   │   │   │   │   │   ├── DashboardHome.tsx
│   │   │   │   │   │   ├── HODashboardView.tsx
│   │   │   │   │   │   └── SharedDashboardView.tsx
│   │   │   │   │   ├── minutes/
│   │   │   │   │   │   ├── MinuteManagement.tsx
│   │   │   │   │   ├── news/
│   │   │   │   │   │   ├── CreateNews.tsx
│   │   │   │   │   │   ├── EditNews.tsx
│   │   │   │   │   │   ├── NewsDetailDashboard.tsx
│   │   │   │   │   │   └── NewsManagement.tsx
│   │   │   │   │   ├── partners/
│   │   │   │   │   │   ├── CreatePartner.tsx
│   │   │   │   │   │   ├── PartnerDetail.tsx
│   │   │   │   │   │   └── PartnerManagement.tsx
│   │   │   │   │   ├── permissions/
│   │   │   │   │   │   ├── PermissionManagement.tsx
│   │   │   │   │   ├── profile/
│   │   │   │   │   │   ├── Profile.tsx
│   │   │   │   │   ├── reports/
│   │   │   │   │   │   ├── DeptReportManagement.tsx
│   │   │   │   │   │   ├── mockReportData.ts
│   │   │   │   │   │   └── ReportManagement.tsx
│   │   │   │   │   └── visit/
│   │   │   │   │       ├── AgendaTemplateManagement.tsx
│   │   │   │   │       ├── CreateVisitRequest.tsx
│   │   │   │   │       ├── HoVisitProcessDetail.tsx
│   │   │   │   │       ├── VisitAfterTab.tsx
│   │   │   │   │       ├── VisitDuringTab.tsx
│   │   │   │   │       ├── VisitProcess.tsx
│   │   │   │   │       ├── VisitRequestDetail.tsx
│   │   │   │   │       └── VisitRequestManagement.tsx
│   │   │   │   ├── FAQPage.tsx
│   │   │   │   ├── ForbiddenPage.tsx
│   │   │   │   ├── HomePage.tsx
│   │   │   │   ├── NewsDetailPage.tsx
│   │   │   │   ├── NewsPage.tsx
│   │   │   │   ├── PartnerDetailPage.tsx
│   │   │   │   ├── PartnersPage.tsx
│   │   │   │   └── VisitFPTUPage.tsx
│   │   │   ├── shared/
│   │   │   │   ├── api/
│   │   │   │   │   ├── authInterceptor.ts
│   │   │   │   │   ├── endpoints.ts
│   │   │   │   │   ├── errorHandler.ts
│   │   │   │   │   └── httpClient.ts
│   │   │   │   ├── auth/
│   │   │   │   │   ├── AuthContext.tsx
│   │   │   │   │   ├── authStorage.ts
│   │   │   │   │   ├── dashboardRoute.ts
│   │   │   │   │   ├── permissionChecker.ts
│   │   │   │   │   ├── ProtectedRoute.tsx
│   │   │   │   │   └── RoleGuard.tsx
│   │   │   │   ├── constants/
│   │   │   │   │   ├── appRoutes.ts
│   │   │   │   │   ├── permissions.ts
│   │   │   │   │   ├── roles.ts
│   │   │   │   │   ├── statusCodes.ts
│   │   │   │   │   └── ucCodes.ts
│   │   │   │   ├── hooks/
│   │   │   │   │   ├── useApiError.ts
│   │   │   │   │   ├── useAuth.ts
│   │   │   │   │   ├── useDebounce.ts
│   │   │   │   │   ├── usePagination.ts
│   │   │   │   │   └── usePermission.ts
│   │   │   │   ├── types/
│   │   │   │   │   ├── api.types.ts
│   │   │   │   │   ├── auth.types.ts
│   │   │   │   │   ├── common.types.ts
│   │   │   │   │   ├── pagination.types.ts
│   │   │   │   │   └── permission.types.ts
│   │   │   │   └── utils/
│   │   │   │       ├── dateUtils.ts
│   │   │   │       ├── fileUtils.ts
│   │   │   │       ├── formatUtils.ts
│   │   │   │       ├── passwordPolicy.ts
│   │   │   │       ├── routeUtils.ts
│   │   │   │       └── validationUtils.ts
│   │   │   ├── types.ts
│   │   │   └── vite-env.d.ts
│   │   ├── transform.cjs
│   │   ├── transform_editable.cjs
│   │   ├── transform_setup_editable.cjs
│   │   ├── tsconfig.json
│   │   ├── updateHeaders.cjs
│   │   └── vite.config.ts
├── payload.json
├── PEMS.slnx
├── README.md
├── scripts/
│   ├── guard-project-structure.ps1
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
    │   │   ├── SearchandFilterDepartmentsQueryTests.cs
    │   │   ├── SearchCoordinationTasksQueryTests.cs
    │   │   ├── SearchPersonnelQueryTests.cs
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
    │   │   ├── ConfigureRolePermissionsCommandHandlerTests.cs
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
    │   │   ├── DatabaseTest.cs
    │   └── Security/
    │       ├── OwnershipCheckerTests.cs
    │       └── PermissionCheckerTests.cs
    └── PEMS.UnitTests/
        ├── Application/
        │   ├── ApplicationDummyTest.cs
        ├── Domain/
        │   ├── DomainDummyTest.cs
        └── SharedKernel/
            ├── SharedKernelDummyTest.cs

`

## 2. Directory Rules

- **frontend/**: React/Vite/TypeScript frontend application.
- **backend/**: .NET 8 backend application using Clean Architecture (API, Application, Domain, Infrastructure).
- **database/**: MySQL database scripts and seeds.
- **docs/**: Project documentation, architectural blueprints, API specifications.

