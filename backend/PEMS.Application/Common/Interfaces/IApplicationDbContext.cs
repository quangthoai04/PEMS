using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

namespace PEMS.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<Campus> Campuses { get; }
    DbSet<Department> Departments { get; }
    DbSet<User> Users { get; }
    DbSet<UserAuthProvider> UserAuthProviders { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<OtpToken> OtpTokens { get; }
    DbSet<LoginLog> LoginLogs { get; }
    DbSet<SecurityEvent> SecurityEvents { get; }
    DbSet<Partner> Partners { get; }
    DbSet<PartnerTranslation> PartnerTranslations { get; }
    DbSet<PartnerContact> PartnerContacts { get; }
    DbSet<PartnerAlias> PartnerAliases { get; }
    DbSet<VisitGuestPartnerLink> VisitGuestPartnerLinks { get; }
    DbSet<UploadedFile> Files { get; }
    DbSet<Document> Documents { get; }
    DbSet<VisitRequest> VisitRequests { get; }
    DbSet<VisitRequestCampus> VisitRequestCampuses { get; }
    DbSet<VisitGuestMember> VisitGuestMembers { get; }
    DbSet<VisitParticipant> VisitParticipants { get; }
    DbSet<VisitAgenda> VisitAgendas { get; }
    DbSet<VisitLogisticsItem> VisitLogisticsItems { get; }
    DbSet<VisitLogisticsItemHandover> VisitLogisticsItemHandovers { get; }
    DbSet<VisitLogisticsAssignmentAttempt> VisitLogisticsAssignmentAttempts { get; }
    DbSet<VisitInstanceReminderSetting> VisitInstanceReminderSettings { get; }

    // ── Expense Statistics (v11) ─────────────────────────────────────────
    DbSet<VisitExpenseReport> VisitExpenseReports { get; }
    DbSet<VisitExpenseItem> VisitExpenseItems { get; }
    DbSet<VisitExpenseReportEvent> VisitExpenseReportEvents { get; }

    // ── Student visit photo storage (Google Drive, independent from Gallery) ──
    DbSet<VisitPhotoFolder> VisitPhotoFolders { get; }
    DbSet<VisitPhoto> VisitPhotos { get; }

    // ── Per-campus form v2 (percampus_v2_migration) ──────────────────────────
    DbSet<VisitInstanceFormDetail> VisitInstanceFormDetails { get; }
    DbSet<VisitInstanceGuestMember> VisitInstanceGuestMembers { get; }
    DbSet<VisitRequestIdentityChange> VisitRequestIdentityChanges { get; }
    DbSet<VisitRequestIdentityChangeEvent> VisitRequestIdentityChangeEvents { get; }
    DbSet<VisitInstanceAmendment> VisitInstanceAmendments { get; }
    DbSet<VisitInstanceAmendmentChange> VisitInstanceAmendmentChanges { get; }
    DbSet<VisitInstanceFormRevisionHistory> VisitInstanceFormRevisionHistories { get; }
    DbSet<VisitRequestRevisionHistory> VisitRequestRevisionHistories { get; }
    DbSet<VisitRequestPendingForm> VisitRequestPendingForms { get; }
    DbSet<VisitRequestFingerprintGuard> VisitRequestFingerprintGuards { get; }
    DbSet<Minute> Minutes { get; }
    DbSet<MinuteActionItem> MinuteActionItems { get; }
    DbSet<MinuteParticipant> MinuteParticipants { get; }
    DbSet<Feedback> Feedbacks { get; }
    DbSet<PEMS.Domain.Entities.News.News> News { get; }
    DbSet<NewsTranslation> NewsTranslations { get; }
    DbSet<NewsContentSection> NewsContentSections { get; }
    DbSet<NewsSectionFile> NewsSectionFiles { get; }
    DbSet<Faq> Faqs { get; }
    DbSet<FaqTranslation> FaqTranslations { get; }
    DbSet<GalleryArea> GalleryAreas { get; }
    DbSet<GalleryLocation> GalleryLocations { get; }
    DbSet<GalleryItem> GalleryItems { get; }
    DbSet<GalleryItemContent> GalleryItemContents { get; }
    DbSet<GalleryItemMedia> GalleryItemMedia { get; }
    DbSet<PhotoFaceTag> PhotoFaceTags { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<SentEmail> SentEmails { get; }
    DbSet<SentEmailRecipient> SentEmailRecipients { get; }
    DbSet<SentEmailAttachment> SentEmailAttachments { get; }
    DbSet<EmailActionToken> EmailActionTokens { get; }

    /// <summary>Send reservations for the six report/invoice actions (G11 / R-103).</summary>
    DbSet<EmailSendIdempotency> EmailSendIdempotencies { get; }

    /// <summary>
    /// Per-template / campus / department / system policy for the reply-contact block. Policy only —
    /// the addresses themselves are read from users, campuses and departments at send time.
    /// </summary>
    DbSet<EmailContactPolicy> EmailContactPolicies { get; }
    DbSet<AccountEmailConfirmation> AccountEmailConfirmations { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }
    DbSet<ApiConfiguration> ApiConfigurations { get; }
    DbSet<ApiUsageQuota> ApiUsageQuotas { get; }
    DbSet<ApiRequestLog> ApiRequestLogs { get; }
    DbSet<BusinessCardOcrJob> BusinessCardOcrJobs { get; }
    DbSet<VisitPhotoFaceScan> VisitPhotoFaceScans { get; }
    DbSet<VisitPhotoFaceDetection> VisitPhotoFaceDetections { get; }
    DbSet<AgendaTemplate> AgendaTemplates { get; }
    DbSet<AgendaTemplateItem> AgendaTemplateItems { get; }
    DbSet<AgendaTemplateDefault> AgendaTemplateDefaults { get; }
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>
    /// Database facade for raw SQL execution, transaction management, and connection access.
    /// </summary>
    Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction so a multi-step write (e.g. UC-17 submit:
    /// consume OTP → provision visitor → insert request + campuses + guests) commits atomically.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
