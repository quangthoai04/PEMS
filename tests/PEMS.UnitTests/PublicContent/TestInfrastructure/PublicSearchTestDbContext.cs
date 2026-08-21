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

namespace PEMS.UnitTests.PublicContent.TestInfrastructure;

/// <summary>
/// EF Core InMemory stand-in for <see cref="IApplicationDbContext"/> covering the four content surfaces
/// public search reads: news, partners, gallery (item → location → area → campus → media → file) and
/// faqs, each with its translation table. Same pruning approach as
/// <c>PartnersTestDbContext</c>/<c>GalleryTestDbContext</c>: only the aggregates in the slice are public
/// DbSet properties (EF discovers the model from those); everything else is an explicit interface
/// implementation, which EF does not discover, plus explicit Ignore calls for types reachable by
/// navigation from inside the slice.
/// </summary>
public sealed class PublicSearchTestDbContext : DbContext, IApplicationDbContext
{
    public static PublicSearchTestDbContext Create() =>
        new(new DbContextOptionsBuilder<PublicSearchTestDbContext>()
            .UseInMemoryDatabase($"pems-public-search-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public PublicSearchTestDbContext(DbContextOptions<PublicSearchTestDbContext> options) : base(options) { }

    // ── The slice under test ──────────────────────────────────────────────────────────
    public DbSet<PEMS.Domain.Entities.News.News> News => Set<PEMS.Domain.Entities.News.News>();
    public DbSet<NewsTranslation> NewsTranslations => Set<NewsTranslation>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<PartnerTranslation> PartnerTranslations => Set<PartnerTranslation>();
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<FaqTranslation> FaqTranslations => Set<FaqTranslation>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<GalleryArea> GalleryAreas => Set<GalleryArea>();
    public DbSet<GalleryLocation> GalleryLocations => Set<GalleryLocation>();
    public DbSet<GalleryItem> GalleryItems => Set<GalleryItem>();
    public DbSet<GalleryItemMedia> GalleryItemMedia => Set<GalleryItemMedia>();
    public DbSet<GalleryItemContent> GalleryItemContents => Set<GalleryItemContent>();
    public DbSet<UploadedFile> Files => Set<UploadedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Campus navigates to User/Department, and User pulls in the whole domain graph (ambiguous
        // multi-FK entities EF cannot configure without the production context). Cut it at
        // User/Department — search only needs Campus rows to exist for the gallery chain.
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<Department>();
        modelBuilder.Ignore<AccountEmailConfirmation>();
        modelBuilder.Entity<Campus>().Ignore(c => c.IcHeadUser);
        modelBuilder.Entity<Campus>().Ignore(c => c.Departments);
        modelBuilder.Entity<Campus>().Ignore(c => c.Users);

        // Per-campus form v2 entities: composite keys are configured only in the real context, so they
        // fail model validation if discovered here. Public search never reads them.
        modelBuilder.Ignore<VisitInstanceFormDetail>();
        modelBuilder.Ignore<VisitInstanceGuestMember>();
        modelBuilder.Ignore<VisitRequestIdentityChange>();
        modelBuilder.Ignore<VisitRequestIdentityChangeEvent>();
        modelBuilder.Ignore<VisitInstanceAmendment>();
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        modelBuilder.Ignore<VisitExpenseReport>();
        modelBuilder.Ignore<VisitExpenseItem>();
        modelBuilder.Ignore<VisitExpenseReportEvent>();

        modelBuilder.Ignore<Document>();
        modelBuilder.Ignore<NewsContentSection>();
        modelBuilder.Ignore<NewsSectionFile>();
        modelBuilder.Ignore<PartnerContact>();
        modelBuilder.Ignore<PartnerAlias>();
        modelBuilder.Ignore<VisitGuestPartnerLink>();
        modelBuilder.Ignore<PhotoFaceTag>();

        modelBuilder.Entity<Partner>()
            .HasOne(p => p.OwnerCampus).WithMany().HasForeignKey(p => p.OwnerCampusId);
    }

    // ── Aggregates public search never touches (NOT discovered by EF) ─────────────────
    DbSet<Role> IApplicationDbContext.Roles => Set<Role>();
    DbSet<Department> IApplicationDbContext.Departments => Set<Department>();
    DbSet<User> IApplicationDbContext.Users => Set<User>();
    DbSet<UserAuthProvider> IApplicationDbContext.UserAuthProviders => Set<UserAuthProvider>();
    DbSet<UserSession> IApplicationDbContext.UserSessions => Set<UserSession>();
    DbSet<OtpToken> IApplicationDbContext.OtpTokens => Set<OtpToken>();
    DbSet<LoginLog> IApplicationDbContext.LoginLogs => Set<LoginLog>();
    DbSet<SecurityEvent> IApplicationDbContext.SecurityEvents => Set<SecurityEvent>();
    DbSet<PartnerContact> IApplicationDbContext.PartnerContacts => Set<PartnerContact>();
    DbSet<PartnerAlias> IApplicationDbContext.PartnerAliases => Set<PartnerAlias>();
    DbSet<VisitGuestPartnerLink> IApplicationDbContext.VisitGuestPartnerLinks => Set<VisitGuestPartnerLink>();
    DbSet<Document> IApplicationDbContext.Documents => Set<Document>();
    DbSet<VisitRequest> IApplicationDbContext.VisitRequests => Set<VisitRequest>();
    DbSet<VisitRequestCampus> IApplicationDbContext.VisitRequestCampuses => Set<VisitRequestCampus>();
    DbSet<VisitGuestMember> IApplicationDbContext.VisitGuestMembers => Set<VisitGuestMember>();
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
    DbSet<Minute> IApplicationDbContext.Minutes => Set<Minute>();
    DbSet<MinuteActionItem> IApplicationDbContext.MinuteActionItems => Set<MinuteActionItem>();
    DbSet<MinuteParticipant> IApplicationDbContext.MinuteParticipants => Set<MinuteParticipant>();
    DbSet<Feedback> IApplicationDbContext.Feedbacks => Set<Feedback>();
    DbSet<NewsContentSection> IApplicationDbContext.NewsContentSections => Set<NewsContentSection>();
    DbSet<NewsSectionFile> IApplicationDbContext.NewsSectionFiles => Set<NewsSectionFile>();
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
    DbSet<ApiConfiguration> IApplicationDbContext.ApiConfigurations => Set<ApiConfiguration>();
    DbSet<ApiUsageQuota> IApplicationDbContext.ApiUsageQuotas => Set<ApiUsageQuota>();
    DbSet<ApiRequestLog> IApplicationDbContext.ApiRequestLogs => Set<ApiRequestLog>();
    DbSet<BusinessCardOcrJob> IApplicationDbContext.BusinessCardOcrJobs => Set<BusinessCardOcrJob>();
    DbSet<VisitPhotoFaceScan> IApplicationDbContext.VisitPhotoFaceScans => Set<VisitPhotoFaceScan>();
    DbSet<VisitPhotoFaceDetection> IApplicationDbContext.VisitPhotoFaceDetections => Set<VisitPhotoFaceDetection>();
    DbSet<AgendaTemplate> IApplicationDbContext.AgendaTemplates => Set<AgendaTemplate>();
    DbSet<AgendaTemplateItem> IApplicationDbContext.AgendaTemplateItems => Set<AgendaTemplateItem>();
    DbSet<AgendaTemplateDefault> IApplicationDbContext.AgendaTemplateDefaults => Set<AgendaTemplateDefault>();
    DbSet<AuditLog> IApplicationDbContext.AuditLogs => Set<AuditLog>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginSerializedTransactionAsync(
        CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);
}
