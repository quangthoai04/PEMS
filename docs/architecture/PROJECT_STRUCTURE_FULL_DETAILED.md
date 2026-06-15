# Project Structure Full Detailed

## 1. Project Overview

* **Tên dự án**: PEMS (FPT Education Management System)
* **Mục đích hệ thống**: Quản lý các chuyến tham quan (Visit Requests) và điều phối đối tác (Partners) cho FPT Education.
* **Backend**: .NET 9 (C# 13), Clean Architecture, MediatR (CQRS), Entity Framework Core.
* **Frontend**: React 19, TypeScript, Vite.
* **Database**: PostgreSQL.
* **Kiến trúc tổng thể**: Clean Architecture, CQRS.
* **Trạng thái hiện tại**: Scaffold / Pending implementation.

## 2. Full Project Tree

```text
PEMS/
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
│   │   │   │   │   ├── CreateAccountCommandHa.../
│   │   │   │   │   ├── CreateAccountCommandVa.../
│   │   │   │   │   ├── CreateAccountCommand.cs
│   │   │   │   │   └── CreateAccountResponse.cs
│   │   │   │   ├── ManageAccountStatus/
│   │   │   │   │   └── ManageAccountSta.../
│   │   │   │   └── UpdateAccountRole/
│   │   │   │       ├── UpdateAccountRoleC.../
│   │   │   │       └── UpdateAccountRoleR.../
│   │   │   └── Queries/
│   │   │       ├── SearchandFilterAccounts/
│   │   │       │   └── SearchandFilt.../
│   │   │       ├── ViewAccountDetails/
│   │   │       │   └── ViewAccountDetails.../
│   │   │       └── ViewAccountList/
│   │   │           ├── ViewAccountListQueryH.../
│   │   │           ├── ViewAccountListDto.cs
│   │   │           └── ViewAccountListQuery.cs
│   │   ├── AgendaTemplates/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateAgendaTemplate/
│   │   │   │   │   └── CreateAg.../
│   │   │   │   ├── DeleteAgendaTemplate/
│   │   │   │   │   └── DeleteAg.../
│   │   │   │   └── UpdateAgendaTemplate/
│   │   │   │       └── UpdateAg.../
│   │   │   └── Queries/
│   │   │       ├── ViewAgendaTemplateDetail/
│   │   │       │   └── ViewA.../
│   │   │       └── ViewAgendaTemplateList/
│   │   │           └── ViewAge.../
│   │   ├── ApiIntegrations/
│   │   │   ├── Commands/
│   │   │   │   ├── ConfigureRequestLimit/
│   │   │   │   │   └── Configu.../
│   │   │   │   ├── CreateAPIConfiguration/
│   │   │   │   │   └── Create.../
│   │   │   │   ├── DeleteAPIConfiguration/
│   │   │   │   │   └── Delete.../
│   │   │   │   ├── ManageAPIStatus/
│   │   │   │   │   └── ManageAPIStat.../
│   │   │   │   ├── TestAPIConnection/
│   │   │   │   │   └── TestAPIConn.../
│   │   │   │   └── UpdateAPIConfiguration/
│   │   │   │       └── Update.../
│   │   │   └── Queries/
│   │   │       ├── SearchAPILogs/
│   │   │       │   ├── SearchAPILogsQue.../
│   │   │       │   └── SearchAPILogsDto.cs
│   │   │       ├── ViewAPIConfiguration/
│   │   │       │   └── ViewAPICo.../
│   │   │       └── ViewAPILogs/
│   │   │           ├── ViewAPILogsQueryHa.../
│   │   │           ├── ViewAPILogsDto.cs
│   │   │           └── ViewAPILogsQuery.cs
│   │   ├── Authentication/
│   │   │   ├── Commands/
│   │   │   │   ├── ForgotPassword/
│   │   │   │   │   ├── ForgotPasswordC.../
│   │   │   │   │   └── ForgotPasswordR.../
│   │   │   │   ├── LoginViaCredentials/
│   │   │   │   │   └── LoginViaCr.../
│   │   │   │   ├── LoginViaSso/
│   │   │   │   │   ├── LoginViaSsoCommand.../
│   │   │   │   │   ├── LoginViaSsoRespons.../
│   │   │   │   │   └── LoginViaSsoCommand.cs
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
│   │   ├── Calendars/
│   │   │   ├── Commands/
│   │   │   │   ├── AddPersonalEvent/
│   │   │   │   │   ├── AddPersonalEventCo.../
│   │   │   │   │   └── AddPersonalEventRe.../
│   │   │   │   ├── DeletePersonalEvent/
│   │   │   │   │   └── DeletePersonalE.../
│   │   │   │   ├── SwitchViewMode/
│   │   │   │   │   ├── SwitchViewModeComman.../
│   │   │   │   │   └── SwitchViewModeRespon.../
│   │   │   │   └── UpdatePersonalEvent/
│   │   │   │       └── UpdatePersonalE.../
│   │   │   └── Queries/
│   │   │       ├── ViewDepartmentCalendar/
│   │   │       │   └── ViewDepartmen.../
│   │   │       ├── ViewEventDetails/
│   │   │       │   ├── ViewEventDetailsQue.../
│   │   │       │   └── ViewEventDetailsDto.cs
│   │   │       └── ViewMyEvents/
│   │   │           ├── ViewMyEventsQueryHandle.../
│   │   │           ├── ViewMyEventsDto.cs
│   │   │           └── ViewMyEventsQuery.cs
│   │   ├── Campuses/
│   │   │   ├── Commands/
│   │   │   │   ├── AddNewCampus/
│   │   │   │   │   ├── AddNewCampusCommandHand.../
│   │   │   │   │   ├── AddNewCampusCommandVali.../
│   │   │   │   │   ├── AddNewCampusCommand.cs
│   │   │   │   │   └── AddNewCampusResponse.cs
│   │   │   │   ├── AssignCampusLead/
│   │   │   │   │   ├── AssignCampusLeadCom.../
│   │   │   │   │   └── AssignCampusLeadRes.../
│   │   │   │   ├── ManageCampusStatus/
│   │   │   │   │   └── ManageCampusStatu.../
│   │   │   │   └── UpdateCampus/
│   │   │   │       ├── UpdateCampusCommandHand.../
│   │   │   │       ├── UpdateCampusCommandVali.../
│   │   │   │       ├── UpdateCampusCommand.cs
│   │   │   │       └── UpdateCampusResponse.cs
│   │   │   └── Queries/
│   │   │       ├── SearchandFilterCampus/
│   │   │       │   └── SearchandFilter.../
│   │   │       ├── ViewCampusDetails/
│   │   │       │   ├── ViewCampusDetailsDt.../
│   │   │       │   └── ViewCampusDetailsQu.../
│   │   │       └── ViewCampusList/
│   │   │           ├── ViewCampusListQueryHan.../
│   │   │           ├── ViewCampusListDto.cs
│   │   │           └── ViewCampusListQuery.cs
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
│   │   │   │   │   └── Approve.../
│   │   │   │   ├── ApproveResourceRequest/
│   │   │   │   │   └── ApproveRes.../
│   │   │   │   ├── CloseDelegation/
│   │   │   │   │   ├── CloseDelegationCo.../
│   │   │   │   │   └── CloseDelegationRe.../
│   │   │   │   ├── ConfirmParticipation/
│   │   │   │   │   └── ConfirmParti.../
│   │   │   │   ├── ConfirmTheChangeProposal/
│   │   │   │   │   └── ConfirmT.../
│   │   │   │   ├── CreateGuestDelegation/
│   │   │   │   │   └── CreateGuest.../
│   │   │   │   ├── CreateMeetingMinutes/
│   │   │   │   │   └── CreateMeetin.../
│   │   │   │   ├── CreateNewsArticle/
│   │   │   │   │   └── CreateNewsArtic.../
│   │   │   │   ├── CreatePartnerProfile/
│   │   │   │   │   └── CreatePartne.../
│   │   │   │   ├── EditMeetingMinutes/
│   │   │   │   │   └── EditMeetingMin.../
│   │   │   │   ├── PrepareVisitLogistics/
│   │   │   │   │   └── PrepareVisi.../
│   │   │   │   ├── ProcessVisitRequest/
│   │   │   │   │   └── ProcessVisitR.../
│   │   │   │   ├── ProposeResourceModification/
│   │   │   │   │   └── Propo.../
│   │   │   │   ├── ScanBusinessCard/
│   │   │   │   │   └── ScanBusinessCard.../
│   │   │   │   ├── SubmitDelegationFeedback/
│   │   │   │   │   └── SubmitDe.../
│   │   │   │   ├── SubmitVisitRequest/
│   │   │   │   │   └── SubmitVisitReq.../
│   │   │   │   ├── TagFacesOnPhotos/
│   │   │   │   │   └── TagFacesOnPhotos.../
│   │   │   │   ├── UpdateGuestDelegation/
│   │   │   │   │   └── UpdateGuest.../
│   │   │   │   ├── UpdateVisitLogistics/
│   │   │   │   │   └── UpdateVisitL.../
│   │   │   │   ├── UploadAttachedDocuments/
│   │   │   │   │   └── UploadAtt.../
│   │   │   │   └── UploadVisitPhotos/
│   │   │   │       └── UploadVisitPhot.../
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── DelegationsMappingProfile.cs
│   │   │   ├── Queries/
│   │   │   │   ├── SearchDelegations/
│   │   │   │   │   └── SearchDelegation.../
│   │   │   │   ├── ViewGuestDelegationDetails/
│   │   │   │   │   └── ViewGue.../
│   │   │   │   ├── ViewGuestDelegationList/
│   │   │   │   │   └── ViewGuestD.../
│   │   │   │   └── ViewMeetingMinutesDetails/
│   │   │   │       └── ViewMeet.../
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── Departments/
│   │   │   ├── Commands/
│   │   │   │   ├── AddDepartmentPersonnel/
│   │   │   │   │   └── AddDepartm.../
│   │   │   │   ├── AddNewDepartment/
│   │   │   │   │   └── AddNewDepartment.../
│   │   │   │   ├── AssignTasks/
│   │   │   │   │   ├── AssignTasksCommandHan.../
│   │   │   │   │   ├── AssignTasksCommandVal.../
│   │   │   │   │   ├── AssignTasksCommand.cs
│   │   │   │   │   └── AssignTasksResponse.cs
│   │   │   │   ├── ManageDepartmentStatus/
│   │   │   │   │   └── ManageDepa.../
│   │   │   │   ├── ReassignDepartmentLead/
│   │   │   │   │   └── ReassignDe.../
│   │   │   │   ├── RemovePersonnel/
│   │   │   │   │   ├── RemovePersonnelCo.../
│   │   │   │   │   └── RemovePersonnelRe.../
│   │   │   │   ├── ReviewAssignedTasks/
│   │   │   │   │   └── ReviewAssigne.../
│   │   │   │   ├── SignTheServiceDeliveryReport/
│   │   │   │   │   └── Sign.../
│   │   │   │   └── UpdateDepartment/
│   │   │   │       └── UpdateDepartment.../
│   │   │   └── Queries/
│   │   │       ├── SearchandFilterDepartments/
│   │   │       │   └── Searcha.../
│   │   │       ├── SearchCoordinationTasks/
│   │   │       │   └── SearchCoor.../
│   │   │       ├── SearchPersonnel/
│   │   │       │   ├── SearchPersonnelQue.../
│   │   │       │   └── SearchPersonnelDto.cs
│   │   │       ├── ViewCoordinationTasks/
│   │   │       │   └── ViewCoordina.../
│   │   │       ├── ViewDepartmentDetails/
│   │   │       │   └── ViewDepartme.../
│   │   │       ├── ViewDepartmentList/
│   │   │       │   └── ViewDepartmentL.../
│   │   │       └── ViewPersonnelDetails/
│   │   │           └── ViewPersonnel.../
│   │   ├── Documents/
│   │   │   ├── Commands/
│   │   │   └── Queries/
│   │   │       ├── SearchDocuments/
│   │   │       │   ├── SearchDocumentsQuery.../
│   │   │       │   ├── SearchDocumentsDto.cs
│   │   │       │   └── SearchDocumentsQuery.cs
│   │   │       └── ViewDocumentList/
│   │   │           ├── ViewDocumentListQue.../
│   │   │           └── ViewDocumentListDto.cs
│   │   ├── Emails/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateEmailTemplate/
│   │   │   │   │   └── CreateEmailTemplat.../
│   │   │   │   ├── EditEmailContent/
│   │   │   │   │   ├── EditEmailContentComma.../
│   │   │   │   │   └── EditEmailContentRespo.../
│   │   │   │   ├── ReplytoEmail/
│   │   │   │   │   ├── ReplytoEmailCommandHandle.../
│   │   │   │   │   ├── ReplytoEmailCommandValida.../
│   │   │   │   │   ├── ReplytoEmailCommand.cs
│   │   │   │   │   └── ReplytoEmailResponse.cs
│   │   │   │   ├── SendEmail/
│   │   │   │   │   ├── SendEmailCommand.cs
│   │   │   │   │   ├── SendEmailCommandHandler.cs
│   │   │   │   │   ├── SendEmailCommandValidator.cs
│   │   │   │   │   └── SendEmailResponse.cs
│   │   │   │   └── UpdateEmailTemplate/
│   │   │   │       └── UpdateEmailTemplat.../
│   │   │   └── Queries/
│   │   │       ├── ViewEmail/
│   │   │       │   ├── ViewEmailDto.cs
│   │   │       │   ├── ViewEmailQuery.cs
│   │   │       │   └── ViewEmailQueryHandler.cs
│   │   │       ├── ViewEmailTemplateDetail/
│   │   │       │   └── ViewEmailTempla.../
│   │   │       └── ViewEmailTemplateList/
│   │   │           └── ViewEmailTemplate.../
│   │   ├── Faqs/
│   │   │   ├── Commands/
│   │   │   │   ├── ChangeFAQVisibility/
│   │   │   │   │   ├── ChangeFAQVisibilityC.../
│   │   │   │   │   └── ChangeFAQVisibilityR.../
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
│   │   │   ├── Commands/
│   │   │   └── Queries/
│   │   │       ├── SearchAndFilterFeedback/
│   │   │       │   └── SearchAndFil.../
│   │   │       └── ViewFeedbackSummary/
│   │   │           └── ViewFeedbackSumm.../
│   │   ├── Galleries/
│   │   │   ├── Commands/
│   │   │   │   ├── AddGalleryItem/
│   │   │   │   │   ├── AddGalleryItemComman.../
│   │   │   │   │   └── AddGalleryItemRespon.../
│   │   │   │   ├── DeleteGalleryItem/
│   │   │   │   │   └── DeleteGalleryItem.../
│   │   │   │   └── UpdateGalleryItem/
│   │   │   │       └── UpdateGalleryItem.../
│   │   │   └── Queries/
│   │   │       ├── SearchGalleryItems/
│   │   │       │   └── SearchGalleryItem.../
│   │   │       └── ViewGalleryItemList/
│   │   │           └── ViewGalleryItemL.../
│   │   ├── MeetingMinutes/
│   │   │   ├── Commands/
│   │   │   └── Queries/
│   │   │       ├── SearchAndFilterMinutes/
│   │   │       │   └── SearchAn.../
│   │   │       └── ViewMinutesList/
│   │   │           └── ViewMinutesList.../
│   │   ├── News/
│   │   │   ├── Commands/
│   │   │   │   ├── AddMultilingualNews/
│   │   │   │   │   ├── AddMultilingualNewsC.../
│   │   │   │   │   └── AddMultilingualNewsR.../
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
│   │   │   │   │   └── ManageNewsVisibilit.../
│   │   │   │   └── PublishNews/
│   │   │   │       ├── PublishNewsCommand.cs
│   │   │   │       ├── PublishNewsCommandHandler.cs
│   │   │   │       ├── PublishNewsCommandValidator.cs
│   │   │   │       └── PublishNewsResponse.cs
│   │   │   └── Queries/
│   │   │       ├── ViewNewsDetails/
│   │   │       │   ├── ViewNewsDetailsQueryHandl.../
│   │   │       │   ├── ViewNewsDetailsDto.cs
│   │   │       │   └── ViewNewsDetailsQuery.cs
│   │   │       └── ViewNewsList/
│   │   │           ├── ViewNewsListDto.cs
│   │   │           ├── ViewNewsListQuery.cs
│   │   │           └── ViewNewsListQueryHandler.cs
│   │   ├── Notifications/
│   │   │   ├── Commands/
│   │   │   └── Queries/
│   │   ├── Partners/
│   │   │   ├── Commands/
│   │   │   │   ├── EditPartnerInformation/
│   │   │   │   │   └── EditPartnerIn.../
│   │   │   │   └── ProcessPartnerCreationRequest/
│   │   │   │       └── Proces.../
│   │   │   ├── Dtos/
│   │   │   │   └── README.md
│   │   │   ├── Mappings/
│   │   │   │   └── PartnersMappingProfile.cs
│   │   │   ├── Queries/
│   │   │   │   ├── SearchPartners/
│   │   │   │   │   ├── SearchPartnersQueryHan.../
│   │   │   │   │   ├── SearchPartnersDto.cs
│   │   │   │   │   └── SearchPartnersQuery.cs
│   │   │   │   ├── ViewPartnerDetails/
│   │   │   │   │   └── ViewPartnerDetails.../
│   │   │   │   └── ViewPartnerLists/
│   │   │   │       ├── ViewPartnerListsQuer.../
│   │   │   │       └── ViewPartnerListsDto.cs
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── Profiles/
│   │   │   ├── Commands/
│   │   │   │   ├── ChangePassword/
│   │   │   │   │   ├── ChangePasswordCommand.../
│   │   │   │   │   ├── ChangePasswordRespons.../
│   │   │   │   │   └── ChangePasswordCommand.cs
│   │   │   │   └── UpdateProfile/
│   │   │   │       ├── UpdateProfileCommandHa.../
│   │   │   │       ├── UpdateProfileCommandVa.../
│   │   │   │       ├── UpdateProfileCommand.cs
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
│   │   │   │   │   └── SearchInformat.../
│   │   │   │   ├── ViewContactInfo/
│   │   │   │   │   ├── ViewContactInfoD.../
│   │   │   │   │   └── ViewContactInfoQ.../
│   │   │   │   ├── ViewFaq/
│   │   │   │   │   ├── ViewFaqDto.cs
│   │   │   │   │   ├── ViewFaqQuery.cs
│   │   │   │   │   └── ViewFaqQueryHandler.cs
│   │   │   │   ├── ViewGallery/
│   │   │   │   │   ├── ViewGalleryQueryHand.../
│   │   │   │   │   ├── ViewGalleryDto.cs
│   │   │   │   │   └── ViewGalleryQuery.cs
│   │   │   │   ├── ViewHomepage/
│   │   │   │   │   ├── ViewHomepageQueryHa.../
│   │   │   │   │   ├── ViewHomepageDto.cs
│   │   │   │   │   └── ViewHomepageQuery.cs
│   │   │   │   ├── ViewNews/
│   │   │   │   │   ├── ViewNewsDto.cs
│   │   │   │   │   ├── ViewNewsQuery.cs
│   │   │   │   │   └── ViewNewsQueryHandler.cs
│   │   │   │   ├── ViewNotifications/
│   │   │   │   │   └── ViewNotificati.../
│   │   │   │   ├── ViewPartners/
│   │   │   │   │   ├── ViewPartnersQueryHa.../
│   │   │   │   │   ├── ViewPartnersDto.cs
│   │   │   │   │   └── ViewPartnersQuery.cs
│   │   │   │   └── ViewPolicyAndTerms/
│   │   │   │       └── ViewPolicyAnd.../
│   │   │   └── Rules/
│   │   │       └── README.md
│   │   ├── Reports/
│   │   │   ├── Commands/
│   │   │   │   └── ExportStatisticsReport/
│   │   │   │       └── ExportStatisti.../
│   │   │   └── Queries/
│   │   │       ├── FilterDashboardByTime/
│   │   │       │   └── FilterDashboardB.../
│   │   │       └── ViewDashboardStatistics/
│   │   │           └── ViewDashboardS.../
│   │   ├── Roles/
│   │   │   ├── Commands/
│   │   │   │   ├── ConfigureRolePermissions/
│   │   │   │   │   └── ConfigureRoleP.../
│   │   │   │   ├── CreateNewRole/
│   │   │   │   │   ├── CreateNewRoleCommandHandl.../
│   │   │   │   │   ├── CreateNewRoleCommandValid.../
│   │   │   │   │   ├── CreateNewRoleCommand.cs
│   │   │   │   │   └── CreateNewRoleResponse.cs
│   │   │   │   ├── DisableAndDeleteRole/
│   │   │   │   │   └── DisableAndDeleteRo.../
│   │   │   │   └── UpdateRoleDetails/
│   │   │   │       ├── UpdateRoleDetailsComm.../
│   │   │   │       └── UpdateRoleDetailsResp.../
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
│   │   │   │   └── CalendarIntegrationServic.../
│   │   │   ├── FaceRecognition/
│   │   │   │   └── FaceRecognitionSer.../
│   │   │   ├── Ocr/
│   │   │   │   └── OcrService.cs
│   │   │   ├── FaceRecognitionService.cs
│   │   │   └── OcrService.cs
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
│   │   ├── API_SPECIFICATION.md
│   │   └── FRONTEND_BACKEND_CONTRACT_GAP.md
│   ├── architecture/
│   │   ├── PROJECT_STRUCTURE_DETAILED_EXPLANATION.md/
│   │   ├── ARCHITECTURE_GUARD_TEST_REPORT.md
│   │   ├── BACKEND_SCAFFOLD_CLEANUP_REPORT.md
│   │   ├── BACKEND_SCAFFOLD_REPORT.md
│   │   ├── BACKEND_USE_CASE_CLASS_BLUEPRINT.md
│   │   ├── CLEAN_ARCHITECTURE.md
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
│       │   │   │   │   └── accountManagementA.../
│       │   │   │   ├── api/
│       │   │   │   │   └── accountManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useAccountManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── accountManagement.typ.../
│       │   │   ├── agenda-templates/
│       │   │   │   ├── adapters/
│       │   │   │   │   └── agendaTemplatesAdapt.../
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
│       │   │   │   │   └── campusManagementAda.../
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
│       │   │   │   │   └── departmentManag.../
│       │   │   │   ├── api/
│       │   │   │   │   └── departmentManagement.../
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useDepartmentManag.../
│       │   │   │   └── types/
│       │   │   │       └── departmentManageme.../
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
│       │   │   │   │   └── galleryManagementA.../
│       │   │   │   ├── api/
│       │   │   │   │   └── galleryManagementApi.ts
│       │   │   │   ├── hooks/
│       │   │   │   │   └── useGalleryManagement.ts
│       │   │   │   └── types/
│       │   │   │       └── galleryManagement.typ.../
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
│       │   │       │   └── rolePermis.../
│       │   │       ├── api/
│       │   │       │   └── rolePermissionM.../
│       │   │       ├── hooks/
│       │   │       │   └── useRolePermis.../
│       │   │       └── types/
│       │   │           └── rolePermissio.../
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
│       ├── package-lock.json
│       ├── package.json
│       ├── README.md
│       ├── transform_editable.cjs
│       ├── transform_setup_editable.cjs
│       ├── transform.cjs
│       ├── tsconfig.json
│       ├── updateHeaders.cjs
│       └── vite.config.ts
├── Scaffolder/
│   ├── Program.cs
│   └── Scaffolder.csproj
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
│   │   │   ├── SignTheServiceDeliveryReportCommandTest.../
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
│   │   │   └── ConfigureRolePermissionsCommandHandlerT.../
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
└── README.md

```

## 3. Backend Architecture Explanation

### 3.1 PEMS.Domain

* Đây là lõi nghiệp vụ.
* Không phụ thuộc project nào.
* Chứa Entities, Enums, ValueObjects, Domain Events.
* Không được chứa EF Core, DbContext, Repository implementation, Controller.
* Vai trò chính:
  * `Entities`: Định nghĩa các object cốt lõi mang state và behavior.
  * `Enums`: Tập hợp các hằng số.
  * `Events`: Các sự kiện xảy ra trong domain.
  * `ValueObjects`: Các object định danh bởi thuộc tính thay vì ID.
  
* Các file chính trong Domain:
  * `BaseEntity.cs`
  * `AuditableEntity.cs`
  * `SoftDeleteEntity.cs`
  * `DomainEvent.cs`
  * `User.cs`
  * `Role.cs`
  * `Permission.cs`
  * `Delegation.cs`
  * `VisitRequest.cs`
  * `Partner.cs`
  * `Campus.cs`
  * (Các entity khác tương ứng)

### 3.2 PEMS.Application

* Đây là tầng Use Case.
* Chứa Command/Query theo CQRS.
* Chứa Handler xử lý use case.
* Chứa Validator, DTO, Response.
* Chứa interface abstraction trong Common/Interfaces.
* Chỉ reference Domain.
* Không được reference Infrastructure.
* Không được gọi DbContext concrete, Repository concrete, EmailService concrete.

Giải thích rõ:
* **Commands**: dùng cho thao tác ghi/thay đổi trạng thái.
* **Queries**: dùng cho thao tác đọc.
* **Handlers**: là nơi implement use case sau này.
* **Validators**: là nơi kiểm tra input.
* **Responses/DTOs**: là contract trả về API.
* **Interfaces**: là cổng để Application gọi Infrastructure mà không phụ thuộc Infrastructure.

### 3.3 PEMS.Infrastructure

* Đây là tầng kỹ thuật.
* Chứa DbContext, EF Core configuration, Repository implementation.
* Chứa Email, File Storage, Logging, Identity, Rate Limiting, External Services.
* Implement interface do Application định nghĩa.
* Được phép reference Application và Domain.
* Không chứa business logic nghiệp vụ.

Giải thích vai trò:
* **Persistence**: Nơi chứa `ApplicationDbContext.cs` và migration.
* **Repositories**: Nơi implement các interface thao tác DB.
* **Configurations**: EF Core mappings cho Entities.
* **Identity**: Tích hợp xác thực phân quyền.
* **Email / FileStorage**: Dịch vụ gọi mail server, lưu file.
* **ExternalServices**: Giao tiếp API ngoài.
* **Logging**: Ghi log hệ thống.
* **RateLimiting**: Chống spam request.
* **DependencyInjection.cs**: Đăng ký các service này vào DI container.

### 3.4 PEMS.Api

* Đây là Presentation Layer / Entry Point.
* Chứa Controllers, Middleware, Filters, Extensions, Program.cs.
* Controller chỉ nên inject IMediator.
* API là Composition Root, được phép gọi `AddApplication` và `AddInfrastructure`.
* Không được gọi DbContext hoặc Repository trực tiếp.

## 4. Frontend Structure Explanation

* `src/pages`:
  * Chứa các page/màn hình chính.
  * Mỗi file page tương ứng một màn hình route hoặc dashboard.

* `src/features`:
  * Chứa logic theo module nghiệp vụ.
  * Mỗi feature có thể có:
    * `api/`: Giao tiếp API.
    * `hooks/`: Custom hooks chứa logic UI.
    * `types/`: Định nghĩa interface, types.
    * `adapters/`: Xử lý convert data từ API sang UI hoặc ngược lại.

* `src/components`:
  * Chứa component dùng chung hoặc component theo layout.

* `src/shared`:
  * Chứa API client, auth helper, permission checker, constants, utilities nếu có.

* `src/assets`:
  * Chứa ảnh, banner, logo, icon, hình campus, hình giao diện.
  * **Avatar**: Chứa avatar user mẫu.
  * **FPT banner**: Chứa banner quảng cáo, thông tin FPT.
  * **campus images**: Chứa hình ảnh các cơ sở.
  * **logo**: Các định dạng logo dự án.
  * **image/news assets**: Hình ảnh cho tin tức, mockup.

## 5. Test Structure Explanation

### PEMS.ArchitectureTests
* Dùng để khóa Dependency Rule.
* Đảm bảo Domain/Application/Infrastructure/API không reference sai.
* Đảm bảo Controller không inject DbContext/Repository concrete.
* Đảm bảo Handler không inject Infrastructure concrete.

### PEMS.ApplicationTests
* Dùng để test Command/Query/Handler/Validator.
* Nếu test đang Skip thì ghi rõ đang chờ đặc tả UC.

### PEMS.InfrastructureTests
* Dùng để test DbContext, EF Core mapping, Repository implementation.

### PEMS.ApiIntegrationTests
* Dùng để test endpoint API end-to-end nếu có.

## 6. Database Structure Explanation

* `database/scripts`: chứa SQL schema chính.
* `database/seed`: chứa dữ liệu seed.
* `database/migrations`: chứa migration nếu có.

Giải thích quan hệ:
* SQL schema là nền để map sang Domain Entity.
* EF Core Configuration nằm trong Infrastructure.
* Application không thao tác SQL trực tiếp.

## 7. Documentation Structure Explanation

* `docs/architecture`: Chứa các tài liệu về kiến trúc hệ thống, design pattern.
* `docs/api`: Chứa tài liệu về thiết kế API, contract.
* `docs/database`: Chứa schema DB và document.
* `docs/permissions`: Ma trận phân quyền.
* `docs/use-cases`: Đặc tả use case.

Mỗi file trong đây đóng vai trò làm tài liệu hướng dẫn (SSOT) và được giữ lại làm tham chiếu cho các giai đoạn phát triển tiếp theo.

## 8. Dependency Diagram

```text
PEMS.Api
   ↓
PEMS.Application
   ↓
PEMS.Domain

PEMS.Infrastructure
   ↗
PEMS.Application
   ↘
PEMS.Domain
```

* `PEMS.Domain` → no project reference
* `PEMS.Application` → PEMS.Domain
* `PEMS.Infrastructure` → PEMS.Application, PEMS.Domain
* `PEMS.Api` → PEMS.Application, PEMS.Infrastructure

Giải thích:
* Domain là lõi.
* Application chỉ biết Domain và interface abstraction.
* Infrastructure implement interface.
* API là composition root.

## 9. Request Flow Example

```text
Frontend
  ↓ HTTP POST
DelegationsController
  ↓ _mediator.Send(command)
SubmitVisitRequestCommandHandler
  ↓ gọi interface
IDelegationRepository / IApplicationDbContext
  ↓ implementation
DelegationRepository / ApplicationDbContext
  ↓
MySQL Database / PostgreSQL Database
```

Giải thích:
* Frontend gọi API.
* Controller nhận request.
* Controller gửi Command/Query qua MediatR.
* Handler xử lý use case.
* Handler gọi interface.
* Infrastructure implement interface để truy cập DB hoặc service kỹ thuật.
* Database chỉ được truy cập từ Infrastructure.

## 10. How To Add A New Use Case

1. Thêm Command hoặc Query trong Application.
2. Thêm Handler.
3. Thêm Validator.
4. Thêm Response hoặc DTO.
5. Thêm endpoint trong Controller.
6. Nếu cần DB, thêm interface ở Application.
7. Implement interface trong Infrastructure.
8. Viết test.
9. Chạy Architecture Tests.

Nhấn mạnh:
* Không thêm Infrastructure reference vào Application.
* Không inject DbContext vào Controller.
* Không viết business logic trong Infrastructure.

## 11. Important Files

* `Program.cs`: Khởi tạo và cấu hình pipeline của API.
* `PEMS.Api.csproj`: File cấu hình project API.
* `PEMS.Application.csproj`: File cấu hình project Application.
* `PEMS.Domain.csproj`: File cấu hình project Domain.
* `PEMS.Infrastructure.csproj`: File cấu hình project Infrastructure.
* `DependencyInjection.cs` của Application: Nơi đăng ký MediatR, FluentValidation.
* `DependencyInjection.cs` của Infrastructure: Nơi đăng ký DbContext, Services kỹ thuật.
* `IApplicationDbContext.cs`: Interface giao tiếp với DB ở Application.
* `ApplicationDbContext.cs`: Implementation của DbContext ở Infrastructure.
* `Architecture Tests`: Khóa kiến trúc và dependencies.
* `BACKEND_USE_CASE_CLASS_BLUEPRINT.md`: Hướng dẫn các use case.
* `FRONTEND_BACKEND_CONTRACT_GAP.md`: Khoảng trống contract giữa FE và BE.
* `pems_full.sql`: File SQL tạo toàn bộ database table ban đầu.
* `Permission matrix`: Ma trận phân quyền.
* `appsettings.json`: Cấu hình hệ thống (như chuỗi kết nối).
* `package.json`: Khai báo thư viện Frontend.
* `vite.config.ts`: Cấu hình build của frontend (Vite).

## 12. Ignored / Generated Files

* `bin/`, `obj/`: Output build của .NET.
* `node_modules/`: Thư viện npm.
* `.git/`: Dữ liệu git history.
* `.vs/`, `.vscode/`: Cấu hình IDE cá nhân.
* `dist/`, `build/`: Output build production của Frontend.
* `coverage/`, `cache/log/temp files`: File sinh tự động khi test/chạy.

Giải thích:
* Đây là file sinh tự động.
* Không cần đưa vào phân tích kiến trúc.
* Không nên commit lên git.

## 13. Conclusion

* Dự án đang tổ chức theo Clean Architecture scaffold.
* Backend có 135 Use Cases scaffold nếu đúng theo tài liệu.
* Dependency Rule đã đúng nếu Architecture Tests vẫn pass.
* Backend hiện sẵn sàng để implement logic theo từng vertical slice.
* EF Core mapping, business logic và integration frontend/backend cần làm ở giai đoạn tiếp theo.
* Tài liệu này dùng để onboarding, review kiến trúc và làm bản đồ dự án.


## 14. Cleanup Suggestions

| File/Folder | Vấn đề | Đề xuất |
|---|---|---|
| `docs/architecture/PROJECT_STRUCTURE_DETAILED_EXPLANATION.md` | File report cũ, trùng lặp mục đích với báo cáo mới | Xóa file cũ để tránh nhầm lẫn |
| `docs/PEMS_AI_Refactor_Project_Structure_Prompt.md` | File prompt rác | Nên xóa khỏi repo |
| `backend/PEMS.SharedKernel/` | Folder gần như trống, chỉ có README.md | Review lại, nếu không áp dụng pattern SharedKernel thì nên xóa |
| `tests/` | Cấu trúc tests chưa đầy đủ hoặc đang để trống các project con | Bổ sung đầy đủ các project test theo quy chuẩn |
