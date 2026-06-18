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
using PEMS.Domain.Entities.PublicContents;
using PEMS.Domain.Entities.Users;
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext() { }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // ── RBAC ─────────────────────────────────────────────────────────────
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }

    // ── Organisation ──────────────────────────────────────────────────────
    public DbSet<Campus> Campuses { get; set; }
    public DbSet<Department> Departments { get; set; }

    // ── Users + Auth ──────────────────────────────────────────────────────
    public DbSet<User> Users { get; set; }
    public DbSet<UserAuthProvider> UserAuthProviders { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    public DbSet<OtpToken> OtpTokens { get; set; }
    public DbSet<LoginLog> LoginLogs { get; set; }
    public DbSet<SecurityEvent> SecurityEvents { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    // ── Partners ──────────────────────────────────────────────────────────
    public DbSet<Partner> Partners { get; set; }
    public DbSet<PartnerContact> PartnerContacts { get; set; }

    // ── Files + Documents ─────────────────────────────────────────────────
    public DbSet<UploadedFile> Files { get; set; }
    public DbSet<Document> Documents { get; set; }

    // ── Visit / Delegation ────────────────────────────────────────────────
    public DbSet<VisitRequest> VisitRequests { get; set; }
    public DbSet<PendingVisitRequest> PendingVisitRequests { get; set; }
    public DbSet<VisitRequestCampus> VisitRequestCampuses { get; set; }
    public DbSet<VisitGuestMember> VisitGuestMembers { get; set; }
    public DbSet<VisitParticipant> VisitParticipants { get; set; }
    public DbSet<VisitAgenda> VisitAgendas { get; set; }
    public DbSet<VisitLogisticsItem> VisitLogisticsItems { get; set; }
    public DbSet<VisitStatusLog> VisitStatusLogs { get; set; }

    // ── Minutes + Feedback ────────────────────────────────────────────────
    public DbSet<Minute> Minutes { get; set; }
    public DbSet<MinuteActionItem> MinuteActionItems { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }

    // ── News ──────────────────────────────────────────────────────────────
    public DbSet<PEMS.Domain.Entities.News.News> News { get; set; }
    public DbSet<NewsTranslation> NewsTranslations { get; set; }
    public DbSet<NewsContentSection> NewsContentSections { get; set; }
    public DbSet<NewsSectionFile> NewsSectionFiles { get; set; }

    // ── FAQs + Public Content ─────────────────────────────────────────────
    public DbSet<Faq> Faqs { get; set; }
    public DbSet<PEMS.Domain.Entities.PublicContents.PublicContent> PublicContents { get; set; }

    // ── Gallery ───────────────────────────────────────────────────────────
    public DbSet<Gallery> Galleries { get; set; }
    public DbSet<GalleryImage> GalleryImages { get; set; }
    public DbSet<PhotoFaceTag> PhotoFaceTags { get; set; }

    // ── Email + Notification ──────────────────────────────────────────────
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<SentEmail> SentEmails { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    // ── Calendar + Agenda Template ────────────────────────────────────────
    public DbSet<CalendarEvent> CalendarEvents { get; set; }
    public DbSet<AgendaTemplate> AgendaTemplates { get; set; }

    // ── API Integrations ──────────────────────────────────────────────────
    public DbSet<ApiConfiguration> ApiConfigurations { get; set; }
    public DbSet<ApiUsageQuota> ApiUsageQuotas { get; set; }
    public DbSet<ApiRequestLog> ApiRequestLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        // ── Composite PKs ─────────────────────────────────────────────────

        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.SubRole, rp.PermissionId });

        // ── BIGINT AUTO_INCREMENT PKs ─────────────────────────────────────

        modelBuilder.Entity<AuditLog>()
            .Property(e => e.AuditLogId).ValueGeneratedOnAdd();
        modelBuilder.Entity<LoginLog>()
            .Property(e => e.LoginLogId).ValueGeneratedOnAdd();
        modelBuilder.Entity<SecurityEvent>()
            .Property(e => e.SecurityEventId).ValueGeneratedOnAdd();
        modelBuilder.Entity<VisitStatusLog>()
            .Property(e => e.VisitStatusLogId).ValueGeneratedOnAdd();
        modelBuilder.Entity<ApiRequestLog>()
            .Property(e => e.ApiRequestLogId).ValueGeneratedOnAdd();

        // ── CHAR(36) column types ─────────────────────────────────────────

        modelBuilder.Entity<Role>(b =>
        {
            b.Property(e => e.RoleId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DeletedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Permission>(b =>
        {
            b.Property(e => e.PermissionId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<RolePermission>(b =>
        {
            b.Property(e => e.RoleId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.PermissionId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.GrantedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Campus>(b =>
        {
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.IcHeadUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Department>(b =>
        {
            b.Property(e => e.DepartmentId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.HeadUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.Property(e => e.UserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RoleId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.PrimaryCampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DepartmentId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<UserAuthProvider>(b =>
        {
            b.Property(e => e.AuthProviderId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UserId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<UserSession>(b =>
        {
            b.Property(e => e.SessionId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.SelectedCampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.AuthProviderId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RevokedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<OtpToken>(b =>
        {
            b.Property(e => e.OtpTokenId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UserId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<LoginLog>(b =>
        {
            b.Property(e => e.UserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.SelectedCampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.SessionId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<SecurityEvent>(b =>
        {
            b.Property(e => e.UserId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.Property(e => e.ActorUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.EntityId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Partner>(b =>
        {
            b.Property(e => e.PartnerId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<PartnerContact>(b =>
        {
            b.Property(e => e.ContactId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.PartnerId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<UploadedFile>(b =>
        {
            b.Property(e => e.FileId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UploadedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Document>(b =>
        {
            b.Property(e => e.DocumentId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.FileId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.OwnerId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<VisitRequest>(b =>
        {
            b.Property(e => e.VisitRequestId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitorUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.PartnerId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DecidedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<VisitRequestCampus>(b =>
        {
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitRequestId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CurrentHostUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.HostTransferredBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ClosedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<VisitGuestMember>(b =>
        {
            b.Property(e => e.GuestMemberId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitRequestId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<VisitParticipant>(b =>
        {
            b.Property(e => e.ParticipantId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.InvitedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.AssignedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<VisitAgenda>(b =>
        {
            b.Property(e => e.AgendaId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ResponsibleUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<VisitLogisticsItem>(b =>
        {
            b.Property(e => e.LogisticsItemId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RequestedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RequestedToDepartmentId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ReceivedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.AssignedToUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.AssignedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ProposedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ProposalRespondedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<VisitStatusLog>(b =>
        {
            b.Property(e => e.VisitRequestId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ChangedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Minute>(b =>
        {
            b.Property(e => e.MinutesId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.FinalizedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Feedback>(b =>
        {
            b.Property(e => e.FeedbackId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitRequestId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.SubmittedByUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.TargetUserId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<PEMS.Domain.Entities.News.News>(b =>
        {
            b.Property(e => e.NewsId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.AuthorUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CoverFileId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DecidedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DeletedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<NewsTranslation>(b =>
        {
            b.Property(e => e.NewsTranslationId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.NewsId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Faq>(b =>
        {
            b.Property(e => e.FaqId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<PEMS.Domain.Entities.PublicContents.PublicContent>(b =>
        {
            b.Property(e => e.PublicContentId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Gallery>(b =>
        {
            b.Property(e => e.GalleryId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DeletedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<GalleryImage>(b =>
        {
            b.Property(e => e.ImageId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.GalleryId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.FileId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DeletedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<PhotoFaceTag>(b =>
        {
            b.Property(e => e.FaceTagId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ImageId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitRequestId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.GuestMemberId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.PartnerContactId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ConfirmedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RemovedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<EmailTemplate>(b =>
        {
            b.Property(e => e.EmailTemplateId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<SentEmail>(b =>
        {
            b.Property(e => e.SentEmailId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.EmailTemplateId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RelatedId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.SentBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.Property(e => e.NotificationId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RecipientUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RelatedId).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<CalendarEvent>(b =>
        {
            b.Property(e => e.CalendarEventId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.OwnerUserId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.VisitInstanceId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.LogisticsItemId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DeletedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<AgendaTemplate>(b =>
        {
            b.Property(e => e.AgendaTemplateId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DeletedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<ApiConfiguration>(b =>
        {
            b.Property(e => e.ApiConfigId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.DeletedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<ApiUsageQuota>(b =>
        {
            b.Property(e => e.ApiUsageQuotaId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.ApiConfigId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
        });

        modelBuilder.Entity<ApiRequestLog>(b =>
        {
            b.Property(e => e.ApiConfigId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CampusId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RequestedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.RelatedId).HasMaxLength(36).IsFixedLength();
        });

        // ── Relationships ─────────────────────────────────────────────────

        // RolePermission
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Role).WithMany(r => r.RolePermissions)
            .HasForeignKey(rp => rp.RoleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission).WithMany(p => p.RolePermissions)
            .HasForeignKey(rp => rp.PermissionId).OnDelete(DeleteBehavior.Cascade);

        // Campus ↔ User (ic_head_user_id)
        modelBuilder.Entity<Campus>()
            .HasOne(c => c.IcHeadUser).WithMany()
            .HasForeignKey(c => c.IcHeadUserId).OnDelete(DeleteBehavior.SetNull);

        // Department → Campus, HeadUser
        modelBuilder.Entity<Department>()
            .HasOne(d => d.Campus).WithMany(c => c.Departments)
            .HasForeignKey(d => d.CampusId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Department>()
            .HasOne(d => d.HeadUser).WithMany()
            .HasForeignKey(d => d.HeadUserId).OnDelete(DeleteBehavior.SetNull);

        // User → Role, Campus, Department
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role).WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<User>()
            .HasOne(u => u.PrimaryCampus).WithMany(c => c.Users)
            .HasForeignKey(u => u.PrimaryCampusId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<User>()
            .HasOne(u => u.Department).WithMany(d => d.Users)
            .HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        // UserAuthProvider → User
        modelBuilder.Entity<UserAuthProvider>()
            .HasOne(p => p.User).WithMany(u => u.AuthProviders)
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

        // UserSession → User, Campus, AuthProvider, RevokedBy
        modelBuilder.Entity<UserSession>()
            .HasOne(s => s.User).WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<UserSession>()
            .HasOne(s => s.AuthProvider).WithMany()
            .HasForeignKey(s => s.AuthProviderId).OnDelete(DeleteBehavior.SetNull);

        // OtpToken → User
        modelBuilder.Entity<OtpToken>()
            .HasOne(o => o.User).WithMany()
            .HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);

        // LoginLog → User
        modelBuilder.Entity<LoginLog>()
            .HasOne(l => l.User).WithMany()
            .HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.SetNull);

        // SecurityEvent → User
        modelBuilder.Entity<SecurityEvent>()
            .HasOne(e => e.User).WithMany()
            .HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.SetNull);

        // AuditLog → User, Campus
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.ActorUser).WithMany()
            .HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.SetNull);

        // PartnerContact → Partner
        modelBuilder.Entity<PartnerContact>()
            .HasOne(pc => pc.Partner).WithMany(p => p.Contacts)
            .HasForeignKey(pc => pc.PartnerId).OnDelete(DeleteBehavior.Restrict);

        // UploadedFile (uploaded_by → User, SET NULL)
        modelBuilder.Entity<UploadedFile>()
            .HasOne<User>().WithMany()
            .HasForeignKey(f => f.UploadedBy).OnDelete(DeleteBehavior.SetNull);

        // Document → UploadedFile
        modelBuilder.Entity<Document>()
            .HasOne(d => d.File).WithMany(f => f.Documents)
            .HasForeignKey(d => d.FileId).OnDelete(DeleteBehavior.Restrict);

        // VisitRequest → Partner, VisitorUser, DecidedBy
        modelBuilder.Entity<VisitRequest>()
            .HasOne(v => v.Partner).WithMany()
            .HasForeignKey(v => v.PartnerId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitRequest>()
            .HasOne<User>().WithMany()
            .HasForeignKey(v => v.VisitorUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitRequest>()
            .HasOne<User>().WithMany()
            .HasForeignKey(v => v.DecidedBy).OnDelete(DeleteBehavior.SetNull);

        // VisitRequestCampus → VisitRequest, Campus, CurrentHostUser, HostTransferredBy, ClosedBy
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne(vc => vc.VisitRequest).WithMany(v => v.CampusInstances)
            .HasForeignKey(vc => vc.VisitRequestId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(vc => vc.CampusId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vc => vc.CurrentHostUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vc => vc.HostTransferredBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vc => vc.ClosedBy).OnDelete(DeleteBehavior.SetNull);

        // VisitGuestMember → VisitRequest
        modelBuilder.Entity<VisitGuestMember>()
            .HasOne(g => g.VisitRequest).WithMany(v => v.GuestMembers)
            .HasForeignKey(g => g.VisitRequestId).OnDelete(DeleteBehavior.Restrict);

        // VisitParticipant → VisitRequestCampus, User, InvitedBy, AssignedBy
        modelBuilder.Entity<VisitParticipant>()
            .HasOne(vp => vp.VisitInstance).WithMany(vc => vc.Participants)
            .HasForeignKey(vp => vp.VisitInstanceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitParticipant>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vp => vp.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitParticipant>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vp => vp.InvitedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitParticipant>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vp => vp.AssignedBy).OnDelete(DeleteBehavior.SetNull);

        // VisitAgenda → VisitRequestCampus, ResponsibleUser
        modelBuilder.Entity<VisitAgenda>()
            .HasOne(va => va.VisitInstance).WithMany(vc => vc.Agendas)
            .HasForeignKey(va => va.VisitInstanceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitAgenda>()
            .HasOne<User>().WithMany()
            .HasForeignKey(va => va.ResponsibleUserId).OnDelete(DeleteBehavior.SetNull);

        // VisitLogisticsItem → VisitRequestCampus + multiple user FKs
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne(li => li.VisitInstance).WithMany(vc => vc.LogisticsItems)
            .HasForeignKey(li => li.VisitInstanceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(li => li.RequestedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne<Department>().WithMany()
            .HasForeignKey(li => li.RequestedToDepartmentId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(li => li.ReceivedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(li => li.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(li => li.AssignedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(li => li.ProposedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(li => li.ProposalRespondedBy).OnDelete(DeleteBehavior.SetNull);

        // VisitStatusLog → VisitRequest, VisitRequestCampus, ChangedBy
        modelBuilder.Entity<VisitStatusLog>()
            .HasOne(vsl => vsl.VisitRequest).WithMany(v => v.StatusLogs)
            .HasForeignKey(vsl => vsl.VisitRequestId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitStatusLog>()
            .HasOne(vsl => vsl.VisitInstance).WithMany(vc => vc.StatusLogs)
            .HasForeignKey(vsl => vsl.VisitInstanceId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitStatusLog>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vsl => vsl.ChangedBy).OnDelete(DeleteBehavior.SetNull);

        // Minute → VisitRequestCampus, CreatedBy, FinalizedBy, EditingBy
        modelBuilder.Entity<Minute>()
            .HasOne<VisitRequestCampus>().WithMany()
            .HasForeignKey(m => m.VisitInstanceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Minute>()
            .HasOne<User>().WithMany()
            .HasForeignKey(m => m.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Minute>()
            .HasOne<User>().WithMany()
            .HasForeignKey(m => m.FinalizedBy).OnDelete(DeleteBehavior.SetNull);

        // Feedback → VisitRequest, VisitRequestCampus, SubmittedBy, GuestMember, ReviewedBy
        modelBuilder.Entity<Feedback>()
            .HasOne<VisitRequest>().WithMany()
            .HasForeignKey(f => f.VisitRequestId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<VisitRequestCampus>().WithMany()
            .HasForeignKey(f => f.VisitInstanceId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Feedback>()
            .HasOne<User>().WithMany()
            .HasForeignKey(f => f.SubmittedByUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Feedback>()
            .HasOne<User>().WithMany()
            .HasForeignKey(f => f.TargetUserId).OnDelete(DeleteBehavior.SetNull);

        // News → Campus, AuthorUser, CoverFile, DecidedBy
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(n => n.CampusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<User>().WithMany()
            .HasForeignKey(n => n.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<UploadedFile>().WithMany()
            .HasForeignKey(n => n.CoverFileId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<User>().WithMany()
            .HasForeignKey(n => n.DecidedBy).OnDelete(DeleteBehavior.SetNull);

        // NewsTranslation → News
        modelBuilder.Entity<NewsTranslation>()
            .HasOne(nt => nt.News).WithMany(n => n.Translations)
            .HasForeignKey(nt => nt.NewsId).OnDelete(DeleteBehavior.Cascade);

        // PublicContent → Campus
        modelBuilder.Entity<PEMS.Domain.Entities.PublicContents.PublicContent>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(pc => pc.CampusId).OnDelete(DeleteBehavior.SetNull);

        // Gallery → Campus, VisitRequestCampus
        modelBuilder.Entity<Gallery>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(g => g.CampusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Gallery>()
            .HasOne<VisitRequestCampus>().WithMany()
            .HasForeignKey(g => g.VisitInstanceId).OnDelete(DeleteBehavior.SetNull);

        // GalleryImage → Gallery, File
        modelBuilder.Entity<GalleryImage>()
            .HasOne(gi => gi.Gallery).WithMany(g => g.Images)
            .HasForeignKey(gi => gi.GalleryId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GalleryImage>()
            .HasOne<UploadedFile>().WithMany()
            .HasForeignKey(gi => gi.FileId).OnDelete(DeleteBehavior.Restrict);

        // PhotoFaceTag → GalleryImage, VisitRequest, GuestMember, PartnerContact, ConfirmedBy
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.Image).WithMany()
            .HasForeignKey(ft => ft.ImageId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne<VisitRequest>().WithMany()
            .HasForeignKey(ft => ft.VisitRequestId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne<VisitGuestMember>().WithMany()
            .HasForeignKey(ft => ft.GuestMemberId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne<PartnerContact>().WithMany()
            .HasForeignKey(ft => ft.PartnerContactId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne<User>().WithMany()
            .HasForeignKey(ft => ft.ConfirmedBy).OnDelete(DeleteBehavior.SetNull);

        // SentEmail → EmailTemplate, SentBy
        modelBuilder.Entity<SentEmail>()
            .HasOne(se => se.EmailTemplate).WithMany()
            .HasForeignKey(se => se.EmailTemplateId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SentEmail>()
            .HasOne<User>().WithMany()
            .HasForeignKey(se => se.SentBy).OnDelete(DeleteBehavior.SetNull);

        // Notification → RecipientUser
        modelBuilder.Entity<Notification>()
            .HasOne<User>().WithMany()
            .HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Cascade);

        // CalendarEvent → OwnerUser, Campus, VisitInstance, LogisticsItem
        modelBuilder.Entity<CalendarEvent>()
            .HasOne<User>().WithMany()
            .HasForeignKey(ce => ce.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CalendarEvent>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(ce => ce.CampusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CalendarEvent>()
            .HasOne<VisitRequestCampus>().WithMany()
            .HasForeignKey(ce => ce.VisitInstanceId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<CalendarEvent>()
            .HasOne<VisitLogisticsItem>().WithMany()
            .HasForeignKey(ce => ce.LogisticsItemId).OnDelete(DeleteBehavior.SetNull);

        // AgendaTemplate → Campus
        modelBuilder.Entity<AgendaTemplate>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(at => at.CampusId).OnDelete(DeleteBehavior.SetNull);

        // ApiUsageQuota → ApiConfiguration, Campus
        modelBuilder.Entity<ApiUsageQuota>()
            .HasOne(q => q.ApiConfiguration).WithMany(c => c.UsageQuotas)
            .HasForeignKey(q => q.ApiConfigId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ApiUsageQuota>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(q => q.CampusId).OnDelete(DeleteBehavior.Cascade);

        // ApiRequestLog → ApiConfiguration, Campus, RequestedBy
        modelBuilder.Entity<ApiRequestLog>()
            .HasOne(rl => rl.ApiConfiguration).WithMany(c => c.RequestLogs)
            .HasForeignKey(rl => rl.ApiConfigId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ApiRequestLog>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(rl => rl.CampusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<ApiRequestLog>()
            .HasOne<User>().WithMany()
            .HasForeignKey(rl => rl.RequestedBy).OnDelete(DeleteBehavior.SetNull);
        // -- JSON columns --------------------------------------------------
        modelBuilder.Entity<PEMS.Domain.Entities.PublicContents.PublicContent>().Property(x => x.TranslationsJson).HasColumnType("json");
        modelBuilder.Entity<EmailTemplate>().Property(x => x.TranslationsJson).HasColumnType("json");
        modelBuilder.Entity<SentEmail>().Property(x => x.RecipientsJson).HasColumnType("json");
        modelBuilder.Entity<CalendarEvent>().Property(x => x.AttendeesJson).HasColumnType("json");
        modelBuilder.Entity<CalendarEvent>().Property(x => x.RemindersJson).HasColumnType("json");
        modelBuilder.Entity<ApiConfiguration>().Property(x => x.CredentialsJson).HasColumnType("json");
        modelBuilder.Entity<ApiConfiguration>().Property(x => x.HeadersJson).HasColumnType("json");
        modelBuilder.Entity<ApiConfiguration>().Property(x => x.BodyTemplateJson).HasColumnType("json");
        modelBuilder.Entity<ApiConfiguration>().Property(x => x.SettingsJson).HasColumnType("json");
        modelBuilder.Entity<AgendaTemplate>().Property(x => x.ItemsJson).HasColumnType("json");

        // -- New entities relationships & CHAR(36) -------------------------
        modelBuilder.Entity<MinuteActionItem>(b => {
            b.Property(e => e.ActionItemId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.MinutesId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.CreatedBy).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.UpdatedBy).HasMaxLength(36).IsFixedLength();
            b.HasOne(a => a.Minute).WithMany(m => m.ActionItems).HasForeignKey(a => a.MinutesId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NewsContentSection>(b => {
            b.Property(e => e.SectionId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.NewsId).HasMaxLength(36).IsFixedLength();
            b.HasOne(s => s.News).WithMany(n => n.Sections).HasForeignKey(s => s.NewsId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NewsSectionFile>(b => {
            b.Property(e => e.SectionFileId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.SectionId).HasMaxLength(36).IsFixedLength();
            b.Property(e => e.FileId).HasMaxLength(36).IsFixedLength();
            b.HasOne(f => f.Section).WithMany(s => s.SectionFiles).HasForeignKey(f => f.SectionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(f => f.File).WithMany().HasForeignKey(f => f.FileId).OnDelete(DeleteBehavior.Restrict);
        });

    }
}




