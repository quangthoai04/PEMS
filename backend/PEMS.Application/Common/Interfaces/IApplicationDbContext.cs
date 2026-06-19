using Microsoft.EntityFrameworkCore;
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
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
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
    DbSet<Minute> Minutes { get; }
    DbSet<MinuteActionItem> MinuteActionItems { get; }
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
    DbSet<Notification> Notifications { get; }
    DbSet<CalendarEvent> CalendarEvents { get; }
    DbSet<ApiConfiguration> ApiConfigurations { get; }
    DbSet<ApiUsageQuota> ApiUsageQuotas { get; }
    DbSet<ApiRequestLog> ApiRequestLogs { get; }
    DbSet<AgendaTemplate> AgendaTemplates { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<VisitStatusLog> VisitStatusLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
