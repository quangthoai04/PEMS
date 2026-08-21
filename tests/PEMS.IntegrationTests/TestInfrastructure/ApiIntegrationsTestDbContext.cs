using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Entities.AgendaTemplates;
using PEMS.Domain.Entities.ApiIntegrations;
using PEMS.Domain.Entities.Calendar;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using PEMS.Domain.Entities.Departments;
using PEMS.Domain.Entities.Documents;
using PEMS.Domain.Entities.Emails;
using PEMS.Domain.Entities.Faqs;
using PEMS.Domain.Entities.Feedbacks;
using PEMS.Domain.Entities.Galleries;
using PEMS.Domain.Entities.Minutes;
using PEMS.Domain.Entities.News;
using PEMS.Domain.Entities.Notifications;
using PEMS.Domain.Entities.Partners;
using PEMS.Domain.Entities.Users;

namespace PEMS.IntegrationTests.TestInfrastructure;

/// <summary>
/// EF Core InMemory stand-in for <see cref="IApplicationDbContext"/> covering the API-integrations slice:
/// the <c>api_configurations</c> row that holds the Google Drive credential, the request log the connection
/// test writes, and the audit trail a reconnect must leave behind.
///
/// <para>
/// Same pruning approach as <c>GalleryTestDbContext</c>, and for the same reason: EF discovers entity types
/// from ALL DbSet properties on the context, including the explicit interface implementations below, so
/// every aggregate outside the slice must be <c>Ignore</c>d explicitly or the model tries to build the whole
/// schema — foreign keys, navigations and all — for three tables' worth of test.
/// </para>
/// </summary>
public sealed class ApiIntegrationsTestDbContext : DbContext, IApplicationDbContext
{
    public static ApiIntegrationsTestDbContext Create() =>
        new(new DbContextOptionsBuilder<ApiIntegrationsTestDbContext>()
            .UseInMemoryDatabase($"pems-api-integrations-{Guid.NewGuid():N}")
            // The OAuth callback commits its credential write and its audit row in one transaction. The
            // InMemory provider has no transactions and warns instead; suppressing it keeps the handler's
            // real code path under test rather than a branch written for the double.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public ApiIntegrationsTestDbContext(DbContextOptions<ApiIntegrationsTestDbContext> options)
        : base(options) { }

    // ── Mapped aggregates (the API-integrations slice) ───────────────────────
    public DbSet<ApiConfiguration> ApiConfigurations => Set<ApiConfiguration>();
    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Navigations that would drag the rest of the schema in behind the three mapped roots.
        modelBuilder.Entity<ApiConfiguration>()
            .Ignore(c => c.UsageQuotas)
            .Ignore(c => c.Headers);
        modelBuilder.Entity<ApiRequestLog>();
        modelBuilder.Entity<AuditLog>()
            .Ignore(a => a.ActorUser)
            .Ignore(a => a.Changes);

        modelBuilder.Ignore<Role>();
        modelBuilder.Ignore<Campus>();
        modelBuilder.Ignore<Department>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<UserAuthProvider>();
        modelBuilder.Ignore<UserSession>();
        modelBuilder.Ignore<OtpToken>();
        modelBuilder.Ignore<LoginLog>();
        modelBuilder.Ignore<SecurityEvent>();
        modelBuilder.Ignore<Partner>();
        modelBuilder.Ignore<PartnerTranslation>();
        modelBuilder.Ignore<PartnerContact>();
        modelBuilder.Ignore<PartnerAlias>();
        modelBuilder.Ignore<VisitGuestPartnerLink>();
        modelBuilder.Ignore<UploadedFile>();
        modelBuilder.Ignore<Document>();
        modelBuilder.Ignore<VisitRequest>();
        modelBuilder.Ignore<VisitRequestCampus>();
        modelBuilder.Ignore<VisitGuestMember>();
        modelBuilder.Ignore<VisitParticipant>();
        modelBuilder.Ignore<VisitAgenda>();
        modelBuilder.Ignore<VisitLogisticsItem>();
        modelBuilder.Ignore<VisitLogisticsItemHandover>();
        modelBuilder.Ignore<VisitLogisticsAssignmentAttempt>();
        modelBuilder.Ignore<VisitInstanceReminderSetting>();
        modelBuilder.Ignore<VisitExpenseReport>();
        modelBuilder.Ignore<VisitExpenseItem>();
        modelBuilder.Ignore<VisitExpenseReportEvent>();
        modelBuilder.Ignore<VisitPhotoFolder>();
        modelBuilder.Ignore<VisitPhoto>();
        modelBuilder.Ignore<VisitInstanceFormDetail>();
        modelBuilder.Ignore<VisitInstanceGuestMember>();
        modelBuilder.Ignore<VisitRequestIdentityChange>();
        modelBuilder.Ignore<VisitRequestIdentityChangeEvent>();
        modelBuilder.Ignore<VisitInstanceAmendment>();
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        modelBuilder.Ignore<VisitRequestPendingForm>();
        modelBuilder.Ignore<VisitRequestFingerprintGuard>();
        modelBuilder.Ignore<Minute>();
        modelBuilder.Ignore<MinuteActionItem>();
        modelBuilder.Ignore<MinuteParticipant>();
        modelBuilder.Ignore<Feedback>();
        modelBuilder.Ignore<PEMS.Domain.Entities.News.News>();
        modelBuilder.Ignore<NewsTranslation>();
        modelBuilder.Ignore<NewsContentSection>();
        modelBuilder.Ignore<NewsSectionFile>();
        modelBuilder.Ignore<Faq>();
        modelBuilder.Ignore<FaqTranslation>();
        modelBuilder.Ignore<GalleryArea>();
        modelBuilder.Ignore<GalleryLocation>();
        modelBuilder.Ignore<GalleryItem>();
        modelBuilder.Ignore<GalleryItemContent>();
        modelBuilder.Ignore<GalleryItemMedia>();
        modelBuilder.Ignore<PhotoFaceTag>();
        modelBuilder.Ignore<EmailTemplate>();
        modelBuilder.Ignore<SentEmail>();
        modelBuilder.Ignore<SentEmailRecipient>();
        modelBuilder.Ignore<SentEmailAttachment>();
        modelBuilder.Ignore<EmailActionToken>();
        modelBuilder.Ignore<EmailSendIdempotency>();
        modelBuilder.Ignore<AccountEmailConfirmation>();
        modelBuilder.Ignore<Notification>();
        modelBuilder.Ignore<CalendarEvent>();
        modelBuilder.Ignore<ApiUsageQuota>();
        modelBuilder.Ignore<ApiConfigurationHeader>();
        modelBuilder.Ignore<AuditLogChange>();
        modelBuilder.Ignore<BusinessCardOcrJob>();
        modelBuilder.Ignore<VisitPhotoFaceScan>();
        modelBuilder.Ignore<VisitPhotoFaceDetection>();
        modelBuilder.Ignore<AgendaTemplate>();
        modelBuilder.Ignore<AgendaTemplateItem>();
        modelBuilder.Ignore<AgendaTemplateDefault>();
    }

    // ── Aggregates this slice never touches (NOT discovered by EF) ───────────
    DbSet<Role> IApplicationDbContext.Roles => Set<Role>();
    DbSet<Campus> IApplicationDbContext.Campuses => Set<Campus>();
    DbSet<Department> IApplicationDbContext.Departments => Set<Department>();
    DbSet<User> IApplicationDbContext.Users => Set<User>();
    DbSet<UserAuthProvider> IApplicationDbContext.UserAuthProviders => Set<UserAuthProvider>();
    DbSet<UserSession> IApplicationDbContext.UserSessions => Set<UserSession>();
    DbSet<OtpToken> IApplicationDbContext.OtpTokens => Set<OtpToken>();
    DbSet<LoginLog> IApplicationDbContext.LoginLogs => Set<LoginLog>();
    DbSet<SecurityEvent> IApplicationDbContext.SecurityEvents => Set<SecurityEvent>();
    DbSet<Partner> IApplicationDbContext.Partners => Set<Partner>();
    DbSet<PartnerTranslation> IApplicationDbContext.PartnerTranslations => Set<PartnerTranslation>();
    DbSet<PartnerContact> IApplicationDbContext.PartnerContacts => Set<PartnerContact>();
    DbSet<PartnerAlias> IApplicationDbContext.PartnerAliases => Set<PartnerAlias>();
    DbSet<VisitGuestPartnerLink> IApplicationDbContext.VisitGuestPartnerLinks => Set<VisitGuestPartnerLink>();
    DbSet<UploadedFile> IApplicationDbContext.Files => Set<UploadedFile>();
    DbSet<Document> IApplicationDbContext.Documents => Set<Document>();
    DbSet<VisitRequest> IApplicationDbContext.VisitRequests => Set<VisitRequest>();
    DbSet<VisitRequestCampus> IApplicationDbContext.VisitRequestCampuses => Set<VisitRequestCampus>();
    DbSet<VisitGuestMember> IApplicationDbContext.VisitGuestMembers => Set<VisitGuestMember>();
    DbSet<VisitParticipant> IApplicationDbContext.VisitParticipants => Set<VisitParticipant>();
    DbSet<VisitAgenda> IApplicationDbContext.VisitAgendas => Set<VisitAgenda>();
    DbSet<VisitLogisticsItem> IApplicationDbContext.VisitLogisticsItems => Set<VisitLogisticsItem>();
    DbSet<VisitLogisticsItemHandover> IApplicationDbContext.VisitLogisticsItemHandovers => Set<VisitLogisticsItemHandover>();
    DbSet<VisitLogisticsAssignmentAttempt> IApplicationDbContext.VisitLogisticsAssignmentAttempts => Set<VisitLogisticsAssignmentAttempt>();
    DbSet<VisitInstanceReminderSetting> IApplicationDbContext.VisitInstanceReminderSettings => Set<VisitInstanceReminderSetting>();
    DbSet<VisitExpenseReport> IApplicationDbContext.VisitExpenseReports => Set<VisitExpenseReport>();
    DbSet<VisitExpenseItem> IApplicationDbContext.VisitExpenseItems => Set<VisitExpenseItem>();
    DbSet<VisitExpenseReportEvent> IApplicationDbContext.VisitExpenseReportEvents => Set<VisitExpenseReportEvent>();
    DbSet<VisitPhotoFolder> IApplicationDbContext.VisitPhotoFolders => Set<VisitPhotoFolder>();
    DbSet<VisitPhoto> IApplicationDbContext.VisitPhotos => Set<VisitPhoto>();
    DbSet<VisitInstanceFormDetail> IApplicationDbContext.VisitInstanceFormDetails => Set<VisitInstanceFormDetail>();
    DbSet<VisitInstanceGuestMember> IApplicationDbContext.VisitInstanceGuestMembers => Set<VisitInstanceGuestMember>();
    DbSet<VisitRequestIdentityChange> IApplicationDbContext.VisitRequestIdentityChanges => Set<VisitRequestIdentityChange>();
    DbSet<VisitRequestIdentityChangeEvent> IApplicationDbContext.VisitRequestIdentityChangeEvents => Set<VisitRequestIdentityChangeEvent>();
    DbSet<VisitInstanceAmendment> IApplicationDbContext.VisitInstanceAmendments => Set<VisitInstanceAmendment>();
    DbSet<VisitInstanceAmendmentChange> IApplicationDbContext.VisitInstanceAmendmentChanges => Set<VisitInstanceAmendmentChange>();
    DbSet<VisitInstanceFormRevisionHistory> IApplicationDbContext.VisitInstanceFormRevisionHistories => Set<VisitInstanceFormRevisionHistory>();
    DbSet<VisitRequestRevisionHistory> IApplicationDbContext.VisitRequestRevisionHistories => Set<VisitRequestRevisionHistory>();
    DbSet<VisitRequestPendingForm> IApplicationDbContext.VisitRequestPendingForms => Set<VisitRequestPendingForm>();
    DbSet<VisitRequestFingerprintGuard> IApplicationDbContext.VisitRequestFingerprintGuards => Set<VisitRequestFingerprintGuard>();
    DbSet<Minute> IApplicationDbContext.Minutes => Set<Minute>();
    DbSet<MinuteActionItem> IApplicationDbContext.MinuteActionItems => Set<MinuteActionItem>();
    DbSet<MinuteParticipant> IApplicationDbContext.MinuteParticipants => Set<MinuteParticipant>();
    DbSet<Feedback> IApplicationDbContext.Feedbacks => Set<Feedback>();
    DbSet<PEMS.Domain.Entities.News.News> IApplicationDbContext.News => Set<PEMS.Domain.Entities.News.News>();
    DbSet<NewsTranslation> IApplicationDbContext.NewsTranslations => Set<NewsTranslation>();
    DbSet<NewsContentSection> IApplicationDbContext.NewsContentSections => Set<NewsContentSection>();
    DbSet<NewsSectionFile> IApplicationDbContext.NewsSectionFiles => Set<NewsSectionFile>();
    DbSet<Faq> IApplicationDbContext.Faqs => Set<Faq>();
    DbSet<FaqTranslation> IApplicationDbContext.FaqTranslations => Set<FaqTranslation>();
    DbSet<GalleryArea> IApplicationDbContext.GalleryAreas => Set<GalleryArea>();
    DbSet<GalleryLocation> IApplicationDbContext.GalleryLocations => Set<GalleryLocation>();
    DbSet<GalleryItem> IApplicationDbContext.GalleryItems => Set<GalleryItem>();
    DbSet<GalleryItemContent> IApplicationDbContext.GalleryItemContents => Set<GalleryItemContent>();
    DbSet<GalleryItemMedia> IApplicationDbContext.GalleryItemMedia => Set<GalleryItemMedia>();
    DbSet<PhotoFaceTag> IApplicationDbContext.PhotoFaceTags => Set<PhotoFaceTag>();
    DbSet<EmailTemplate> IApplicationDbContext.EmailTemplates => Set<EmailTemplate>();
    DbSet<SentEmail> IApplicationDbContext.SentEmails => Set<SentEmail>();
    DbSet<SentEmailRecipient> IApplicationDbContext.SentEmailRecipients => Set<SentEmailRecipient>();
    DbSet<SentEmailAttachment> IApplicationDbContext.SentEmailAttachments => Set<SentEmailAttachment>();
    DbSet<EmailActionToken> IApplicationDbContext.EmailActionTokens => Set<EmailActionToken>();
    DbSet<EmailSendIdempotency> IApplicationDbContext.EmailSendIdempotencies => Set<EmailSendIdempotency>();
    DbSet<AccountEmailConfirmation> IApplicationDbContext.AccountEmailConfirmations => Set<AccountEmailConfirmation>();
    DbSet<Notification> IApplicationDbContext.Notifications => Set<Notification>();
    DbSet<CalendarEvent> IApplicationDbContext.CalendarEvents => Set<CalendarEvent>();
    DbSet<ApiUsageQuota> IApplicationDbContext.ApiUsageQuotas => Set<ApiUsageQuota>();
    DbSet<BusinessCardOcrJob> IApplicationDbContext.BusinessCardOcrJobs => Set<BusinessCardOcrJob>();
    DbSet<VisitPhotoFaceScan> IApplicationDbContext.VisitPhotoFaceScans => Set<VisitPhotoFaceScan>();
    DbSet<VisitPhotoFaceDetection> IApplicationDbContext.VisitPhotoFaceDetections => Set<VisitPhotoFaceDetection>();
    DbSet<AgendaTemplate> IApplicationDbContext.AgendaTemplates => Set<AgendaTemplate>();
    DbSet<AgendaTemplateItem> IApplicationDbContext.AgendaTemplateItems => Set<AgendaTemplateItem>();
    DbSet<AgendaTemplateDefault> IApplicationDbContext.AgendaTemplateDefaults => Set<AgendaTemplateDefault>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginSerializedTransactionAsync(
        CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);
}
