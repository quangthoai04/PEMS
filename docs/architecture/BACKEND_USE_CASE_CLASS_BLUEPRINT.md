# Backend Use Case Class Blueprint

| UC Code | UC Name | Module | Type | API Controller | Application Class | Domain Entity | Infrastructure Needed | Permission | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| UC-01 | View Homepage | PublicContent | Query | PublicContentController | ViewHomepageQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-02 | Search Information | PublicContent | Query | PublicContentController | SearchInformationQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-03 | View Contact Info | PublicContent | Query | PublicContentController | ViewContactInfoQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-04 | View Policy & Terms | PublicContent | Query | PublicContentController | ViewPolicyAndTermsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-05 | View FAQ | PublicContent | Query | PublicContentController | ViewFAQQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-06 | View News | PublicContent | Query | PublicContentController | ViewNewsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-07 | View Partners | PublicContent | Query | PublicContentController | ViewPartnersQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-08 | View Gallery | PublicContent | Query | PublicContentController | ViewGalleryQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-09 | View Notifications | PublicContent | Query | PublicContentController | ViewNotificationsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-10 | Login via SSO | Authentication | Command | AuthenticationController | LoginviaSSOCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-11 | Login via Credentials | Authentication | Command | AuthenticationController | LoginviaCredentialsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-12 | Logout | Authentication | Command | AuthenticationController | LogoutCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-13 | Forgot Password | Authentication | Command | AuthenticationController | ForgotPasswordCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-14 | View Profile | Profiles | Query | ProfilesController | ViewProfileQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-15 | Update Profile | Profiles | Command | ProfilesController | UpdateProfileCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-16 | Change Password | Profiles | Command | ProfilesController | ChangePasswordCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-17 | Submit Visit Request | Delegations | Command | DelegationsController | SubmitVisitRequestCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-18 | Approve Cross-Campus Request | Delegations | Command | DelegationsController | ApproveCrossCampusRequestCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-19 | View Guest Delegation Details | Delegations | Query | DelegationsController | ViewGuestDelegationDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-20 | View Guest Delegation List | Delegations | Query | DelegationsController | ViewGuestDelegationListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-21 | Search Delegations | Delegations | Query | DelegationsController | SearchDelegationsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-22 | Process Visit Request | Delegations | Command | DelegationsController | ProcessVisitRequestCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-23 | Create Guest Delegation | Delegations | Command | DelegationsController | CreateGuestDelegationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-24 | Update Guest Delegation | Delegations | Command | DelegationsController | UpdateGuestDelegationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-25 | Prepare Visit Logistics | Delegations | Command | DelegationsController | PrepareVisitLogisticsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-26 | Update Visit Logistics | Delegations | Command | DelegationsController | UpdateVisitLogisticsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-27 | Confirm Participation | Delegations | Command | DelegationsController | ConfirmParticipationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-28 | Approve Resource Request | Delegations | Command | DelegationsController | ApproveResourceRequestCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-29 | Propose Resource Modification | Delegations | Command | DelegationsController | ProposeResourceModificationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-30 | Confirm The Change Proposal | Delegations | Command | DelegationsController | ConfirmTheChangeProposalCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-31 | Create Meeting Minutes | Delegations | Command | DelegationsController | CreateMeetingMinutesCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-32 | Edit Meeting Minutes | Delegations | Command | DelegationsController | EditMeetingMinutesCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-33 | View Meeting Minutes Details | Delegations | Query | DelegationsController | ViewMeetingMinutesDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-34 | Upload Attached Documents | Delegations | Command | DelegationsController | UploadAttachedDocumentsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-35 | Submit Delegation Feedback | Delegations | Command | DelegationsController | SubmitDelegationFeedbackCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-36 | Scan Business Card | Delegations | Command | DelegationsController | ScanBusinessCardCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-37 | Create Partner Profile | Delegations | Command | DelegationsController | CreatePartnerProfileCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-38 | Upload Visit Photos | Delegations | Command | DelegationsController | UploadVisitPhotosCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-39 | Tag Faces on Photos | Delegations | Command | DelegationsController | TagFacesonPhotosCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-40 | Create News Article | Delegations | Command | DelegationsController | CreateNewsArticleCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-41 | Close Delegation | Delegations | Command | DelegationsController | CloseDelegationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-42 | View Email Template List | Emails | Query | EmailsController | ViewEmailTemplateListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-43 | View Email Template Detail | Emails | Query | EmailsController | ViewEmailTemplateDetailQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-44 | Update Email Template | Emails | Command | EmailsController | UpdateEmailTemplateCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-45 | Create Email Template | Emails | Command | EmailsController | CreateEmailTemplateCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-46 | Edit Email Content | Emails | Command | EmailsController | EditEmailContentCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-47 | Send Email | Emails | Command | EmailsController | SendEmailCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-48 | View Email | Emails | Query | EmailsController | ViewEmailQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-49 | Reply to Email | Emails | Command | EmailsController | ReplytoEmailCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-50 | Process Partner Creation Request | Partners | Command | PartnersController | ProcessPartnerCreationRequestCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-51 | Edit Partner Information | Partners | Command | PartnersController | EditPartnerInformationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-52 | View Partner Lists | Partners | Query | PartnersController | ViewPartnerListsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-53 | Search Partners | Partners | Query | PartnersController | SearchPartnersQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-54 | View Partner Details | Partners | Query | PartnersController | ViewPartnerDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-55 | View Document List | Documents | Query | DocumentsController | ViewDocumentListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-56 | Search Documents | Documents | Query | DocumentsController | SearchDocumentsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-57 | View Gallery Item List | Galleries | Query | GalleriesController | ViewGalleryItemListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-58 | Search Gallery Items | Galleries | Query | GalleriesController | SearchGalleryItemsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-59 | Add Gallery Item | Galleries | Command | GalleriesController | AddGalleryItemCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-60 | Update Gallery Item | Galleries | Command | GalleriesController | UpdateGalleryItemCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-61 | Delete Gallery Item | Galleries | Command | GalleriesController | DeleteGalleryItemCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-62 | View Minutes List | MeetingMinutes | Query | MeetingMinutesController | ViewMinutesListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-63 | Search/Filter Minutes | MeetingMinutes | Query | MeetingMinutesController | SearchAndFilterMinutesQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-64 | View List FAQ | Faqs | Query | FaqsController | ViewListFAQQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-65 | Create FAQ | Faqs | Command | FaqsController | CreateFAQCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-66 | Update FAQ | Faqs | Command | FaqsController | UpdateFAQCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-67 | Change FAQ Visibility | Faqs | Command | FaqsController | ChangeFAQVisibilityCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-68 | Search FAQ | Faqs | Query | FaqsController | SearchFAQQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-69 | View Dashboard Statistics | Reports | Query | ReportsController | ViewDashboardStatisticsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-70 | Export Statistics Report | Reports | Command | ReportsController | ExportStatisticsReportCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-71 | Filter Dashboard By Time | Reports | Query | ReportsController | FilterDashboardByTimeQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-72 | View My Events | Calendars | Query | CalendarsController | ViewMyEventsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-73 | View Department Calendar | Calendars | Query | CalendarsController | ViewDepartmentCalendarQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-74 | Switch View Mode | Calendars | Command | CalendarsController | SwitchViewModeCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-75 | Add Personal Event | Calendars | Command | CalendarsController | AddPersonalEventCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-76 | Delete Personal Event | Calendars | Command | CalendarsController | DeletePersonalEventCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-77 | Update Personal Event | Calendars | Command | CalendarsController | UpdatePersonalEventCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-78 | View Event Details | Calendars | Query | CalendarsController | ViewEventDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-79 | Search/Filter Feedback | Feedbacks | Query | FeedbacksController | SearchAndFilterFeedbackQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-80 | View Feedback Summary | Feedbacks | Query | FeedbacksController | ViewFeedbackSummaryQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-81 | Add New Campus | Campuses | Command | CampusesController | AddNewCampusCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-82 | View Campus List | Campuses | Query | CampusesController | ViewCampusListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-83 | Search and Filter Campus | Campuses | Query | CampusesController | SearchandFilterCampusQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-84 | View Campus Details | Campuses | Query | CampusesController | ViewCampusDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-85 | Update Campus | Campuses | Command | CampusesController | UpdateCampusCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-86 | Manage Campus Status | Campuses | Command | CampusesController | ManageCampusStatusCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-87 | Assign Campus Lead | Campuses | Command | CampusesController | AssignCampusLeadCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-88 | Approve News | News | Command | NewsController | ApproveNewsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-89 | Publish News | News | Command | NewsController | PublishNewsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-90 | View News List | News | Query | NewsController | ViewNewsListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-91 | View News Details | News | Query | NewsController | ViewNewsDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-92 | Add Multilingual News | News | Command | NewsController | AddMultilingualNewsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-93 | Manage News Visibility | News | Command | NewsController | ManageNewsVisibilityCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-94 | Edit News | News | Command | NewsController | EditNewsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-95 | View Account List | Accounts | Query | AccountsController | ViewAccountListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-96 | Create Account | Accounts | Command | AccountsController | CreateAccountCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-97 | Manage Account Status | Accounts | Command | AccountsController | ManageAccountStatusCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-98 | View Account Details | Accounts | Query | AccountsController | ViewAccountDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-99 | Search and Filter Accounts | Accounts | Query | AccountsController | SearchandFilterAccountsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-100 | Update Account Role | Accounts | Command | AccountsController | UpdateAccountRoleCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-101 | Add New Department | Departments | Command | DepartmentsController | AddNewDepartmentCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-102 | Update Department | Departments | Command | DepartmentsController | UpdateDepartmentCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-103 | Search and Filter Departments | Departments | Query | DepartmentsController | SearchandFilterDepartmentsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-104 | View Department List | Departments | Query | DepartmentsController | ViewDepartmentListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-105 | View Department Details | Departments | Query | DepartmentsController | ViewDepartmentDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-106 | Manage Department Status | Departments | Command | DepartmentsController | ManageDepartmentStatusCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-107 | Add Department Personnel | Departments | Command | DepartmentsController | AddDepartmentPersonnelCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-108 | View Personnel Details | Departments | Query | DepartmentsController | ViewPersonnelDetailsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-109 | Search Personnel | Departments | Query | DepartmentsController | SearchPersonnelQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-110 | Review Assigned Tasks | Departments | Command | DepartmentsController | ReviewAssignedTasksCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-111 | Assign Tasks | Departments | Command | DepartmentsController | AssignTasksCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-112 | Sign The Service Delivery Report | Departments | Command | DepartmentsController | SignTheServiceDeliveryReportCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-113 | Remove Personnel | Departments | Command | DepartmentsController | RemovePersonnelCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-114 | View Coordination Tasks | Departments | Query | DepartmentsController | ViewCoordinationTasksQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-115 | Search Coordination Tasks | Departments | Query | DepartmentsController | SearchCoordinationTasksQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-116 | Reassign Department Lead | Departments | Command | DepartmentsController | ReassignDepartmentLeadCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-117 | View Role List | Roles | Query | RolesController | ViewRoleListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-118 | Create New Role | Roles | Command | RolesController | CreateNewRoleCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-119 | Configure Role Permissions | Roles | Command | RolesController | ConfigureRolePermissionsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-120 | Update Role Details | Roles | Command | RolesController | UpdateRoleDetailsCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-121 | Disable/Delete Role | Roles | Command | RolesController | DisableAndDeleteRoleCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-122 | View API Configuration | ApiIntegrations | Query | ApiIntegrationsController | ViewAPIConfigurationQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-123 | Create API Configuration | ApiIntegrations | Command | ApiIntegrationsController | CreateAPIConfigurationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-124 | Update API Configuration | ApiIntegrations | Command | ApiIntegrationsController | UpdateAPIConfigurationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-125 | Delete API Configuration | ApiIntegrations | Command | ApiIntegrationsController | DeleteAPIConfigurationCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-126 | Test API Connection | ApiIntegrations | Command | ApiIntegrationsController | TestAPIConnectionCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-127 | Manage API Status | ApiIntegrations | Command | ApiIntegrationsController | ManageAPIStatusCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-128 | Configure Request Limit | ApiIntegrations | Command | ApiIntegrationsController | ConfigureRequestLimitCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-129 | View API Logs | ApiIntegrations | Query | ApiIntegrationsController | ViewAPILogsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-130 | Search API Logs | ApiIntegrations | Query | ApiIntegrationsController | SearchAPILogsQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-131 | Create Agenda Template | AgendaTemplates | Command | AgendaTemplatesController | CreateAgendaTemplateCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-132 | Update Agenda Template | AgendaTemplates | Command | AgendaTemplatesController | UpdateAgendaTemplateCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-133 | Delete Agenda Template | AgendaTemplates | Command | AgendaTemplatesController | DeleteAgendaTemplateCommand | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-134 | View Agenda Template List | AgendaTemplates | Query | AgendaTemplatesController | ViewAgendaTemplateListQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
| UC-135 | View Agenda Template Detail | AgendaTemplates | Query | AgendaTemplatesController | ViewAgendaTemplateDetailQuery | TBD - Need UC specification | DB | TBD | Scaffolded |
