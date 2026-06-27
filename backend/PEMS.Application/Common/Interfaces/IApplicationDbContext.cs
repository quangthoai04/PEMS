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
    DbSet<PartnerContact> PartnerContacts { get; }
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
    DbSet<Minute> Minutes { get; }
    DbSet<MinuteActionItem> MinuteActionItems { get; }
    DbSet<MinuteParticipant> MinuteParticipants { get; }
    DbSet<Feedback> Feedbacks { get; }
    DbSet<PEMS.Domain.Entities.News.News> News { get; }
    DbSet<NewsTranslation> NewsTranslations { get; }
    DbSet<NewsContentSection> NewsContentSections { get; }
    DbSet<NewsSectionFile> NewsSectionFiles { get; }
    DbSet<Faq> Faqs { get; }
    DbSet<Gallery> Galleries { get; }
    DbSet<GalleryImage> GalleryImages { get; }
    DbSet<PhotoFaceTag> PhotoFaceTags { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<SentEmail> SentEmails { get; }
    DbSet<SentEmailRecipient> SentEmailRecipients { get; }
    DbSet<SentEmailAttachment> SentEmailAttachments { get; }
    DbSet<EmailDraft> EmailDrafts { get; }
    DbSet<EmailDraftRecipient> EmailDraftRecipients { get; }
    DbSet<EmailDraftAttachment> EmailDraftAttachments { get; }
    DbSet<EmailActionToken> EmailActionTokens { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }
    DbSet<ApiConfiguration> ApiConfigurations { get; }
    DbSet<ApiUsageQuota> ApiUsageQuotas { get; }
    DbSet<ApiRequestLog> ApiRequestLogs { get; }
    DbSet<AgendaTemplate> AgendaTemplates { get; }
    DbSet<AgendaTemplateItem> AgendaTemplateItems { get; }
    DbSet<AgendaTemplateDefault> AgendaTemplateDefaults { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins an explicit database transaction so a multi-step write (e.g. UC-17 submit:
    /// consume OTP → provision visitor → insert request + campuses + guests) commits atomically.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
