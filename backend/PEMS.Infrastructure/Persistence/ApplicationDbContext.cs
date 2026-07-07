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
using PEMS.Application.Common.Interfaces;

namespace PEMS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext() { }
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    // ── RBAC ─────────────────────────────────────────────────────────────
    public DbSet<Role> Roles { get; set; }

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
    public DbSet<AuditLogChange> AuditLogChanges { get; set; }

    // ── Partners ──────────────────────────────────────────────────────────
    public DbSet<Partner> Partners { get; set; }
    public DbSet<PartnerContact> PartnerContacts { get; set; }
    public DbSet<PartnerAlias> PartnerAliases { get; set; }
    public DbSet<VisitGuestPartnerLink> VisitGuestPartnerLinks { get; set; }

    // ── Files + Documents ─────────────────────────────────────────────────
    public DbSet<UploadedFile> Files { get; set; }
    public DbSet<Document> Documents { get; set; }

    // ── Visit / Delegation ────────────────────────────────────────────────
    // NOTE: no pending_visit_requests table in SQL v8.3 — OTP drafts live in otp_tokens.
    public DbSet<VisitRequest> VisitRequests { get; set; }
    public DbSet<VisitRequestCampus> VisitRequestCampuses { get; set; }
    public DbSet<VisitGuestMember> VisitGuestMembers { get; set; }
    public DbSet<VisitParticipant> VisitParticipants { get; set; }
    public DbSet<VisitAgenda> VisitAgendas { get; set; }
    public DbSet<VisitLogisticsItem> VisitLogisticsItems { get; set; }
    public DbSet<VisitLogisticsItemHandover> VisitLogisticsItemHandovers { get; set; }
    public DbSet<VisitLogisticsAssignmentAttempt> VisitLogisticsAssignmentAttempts { get; set; }
    public DbSet<VisitInstanceReminderSetting> VisitInstanceReminderSettings { get; set; }

    // ── Minutes + Feedback ────────────────────────────────────────────────
    public DbSet<Minute> Minutes { get; set; }
    public DbSet<MinuteActionItem> MinuteActionItems { get; set; }
    public DbSet<MinuteParticipant> MinuteParticipants { get; set; }
    public DbSet<Feedback> Feedbacks { get; set; }
    public DbSet<FeedbackRatingItem> FeedbackRatingItems { get; set; }

    // ── News ──────────────────────────────────────────────────────────────
    public DbSet<PEMS.Domain.Entities.News.News> News { get; set; }
    public DbSet<NewsTranslation> NewsTranslations { get; set; }
    public DbSet<NewsContentSection> NewsContentSections { get; set; }
    public DbSet<NewsSectionFile> NewsSectionFiles { get; set; }

    // ── FAQs ──────────────────────────────────────────────────────────────
    // NOTE: no public_contents table in SQL v8.3 — public pages read from news/faqs/galleries/partners.
    public DbSet<Faq> Faqs { get; set; }

    // ── Gallery ───────────────────────────────────────────────────────────
    public DbSet<GalleryArea> GalleryAreas { get; set; }
    public DbSet<GalleryLocation> GalleryLocations { get; set; }
    public DbSet<GalleryItem> GalleryItems { get; set; }
    public DbSet<GalleryItemMedia> GalleryItemMedia { get; set; }
    public DbSet<GalleryItemTtsAudio> GalleryItemTtsAudios { get; set; }
    public DbSet<PhotoFaceTag> PhotoFaceTags { get; set; }

    // ── Email + Notification ──────────────────────────────────────────────
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<SentEmail> SentEmails { get; set; }
    public DbSet<SentEmailRecipient> SentEmailRecipients { get; set; }
    public DbSet<SentEmailAttachment> SentEmailAttachments { get; set; }
    public DbSet<EmailDraft> EmailDrafts { get; set; }
    public DbSet<EmailDraftRecipient> EmailDraftRecipients { get; set; }
    public DbSet<EmailDraftAttachment> EmailDraftAttachments { get; set; }
    public DbSet<EmailActionToken> EmailActionTokens { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    // ── Calendar + Agenda Template ────────────────────────────────────────
    public DbSet<CalendarEvent> CalendarEvents { get; set; }
    public DbSet<CalendarEventAttendee> CalendarEventAttendees { get; set; }
    public DbSet<CalendarEventReminder> CalendarEventReminders { get; set; }
    public DbSet<AgendaTemplate> AgendaTemplates { get; set; }
    public DbSet<AgendaTemplateItem> AgendaTemplateItems { get; set; }
    public DbSet<AgendaTemplateDefault> AgendaTemplateDefaults { get; set; }

    // ── API Integrations ──────────────────────────────────────────────────
    public DbSet<ApiConfiguration> ApiConfigurations { get; set; }
    public DbSet<ApiConfigurationHeader> ApiConfigurationHeaders { get; set; }
    public DbSet<ApiUsageQuota> ApiUsageQuotas { get; set; }
    public DbSet<ApiRequestLog> ApiRequestLogs { get; set; }
    public DbSet<BusinessCardOcrJob> BusinessCardOcrJobs { get; set; }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<User>()
            .Property(u => u.Gender)
            .HasConversion(
                v => v.ToString().ToUpper(),
                v => (PEMS.Domain.Enums.Gender)Enum.Parse(typeof(PEMS.Domain.Enums.Gender), v, true));



        // All other PKs are BIGINT UNSIGNED AUTO_INCREMENT and are treated as
        // store-generated identity by EF Core convention for integer keys.
        // FK columns (also BIGINT UNSIGNED) are NOT store-generated.

        // ── Relationships ─────────────────────────────────────────────────



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

        // UserSession → User, AuthProvider
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

        // Partner → OwnerCampus (v10)
        modelBuilder.Entity<Partner>()
            .HasOne(p => p.OwnerCampus).WithMany()
            .HasForeignKey(p => p.OwnerCampusId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Partner>()
            .HasIndex(p => new { p.OwnerCampusId, p.ProfileStatus })
            .HasDatabaseName("idx_partners_owner_status");
        modelBuilder.Entity<Partner>()
            .HasIndex(p => new { p.OwnerCampusId, p.CreatedAt })
            .HasDatabaseName("idx_partners_owner_created");

        // PartnerContact → Partner
        modelBuilder.Entity<PartnerContact>()
            .HasOne(pc => pc.Partner).WithMany(p => p.Contacts)
            .HasForeignKey(pc => pc.PartnerId).OnDelete(DeleteBehavior.Restrict);

        // PartnerAlias → Partner (patch_partner_module.sql)
        modelBuilder.Entity<PartnerAlias>()
            .HasOne(a => a.Partner).WithMany()
            .HasForeignKey(a => a.PartnerId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PartnerAlias>()
            .HasIndex(a => new { a.PartnerId, a.AliasNameKey })
            .IsUnique()
            .HasDatabaseName("uq_partner_alias_key");

        // VisitGuestPartnerLink (patch_partner_module.sql)
        modelBuilder.Entity<VisitGuestPartnerLink>()
            .HasOne(l => l.Partner).WithMany()
            .HasForeignKey(l => l.PartnerId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitGuestPartnerLink>()
            .HasOne(l => l.PartnerContact).WithMany()
            .HasForeignKey(l => l.PartnerContactId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitGuestPartnerLink>()
            .HasOne(l => l.VisitRequest).WithMany()
            .HasForeignKey(l => l.VisitRequestId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VisitGuestPartnerLink>()
            .HasOne(l => l.VisitInstance).WithMany()
            .HasForeignKey(l => l.VisitInstanceId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VisitGuestPartnerLink>()
            .HasOne(l => l.GuestMember).WithMany()
            .HasForeignKey(l => l.GuestMemberId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitGuestPartnerLink>()
            .HasOne(l => l.MinuteParticipant).WithMany()
            .HasForeignKey(l => l.MinuteParticipantId).OnDelete(DeleteBehavior.SetNull);

        // BusinessCardOcrJob (patch_business_card_ocr_api_config.sql)
        modelBuilder.Entity<BusinessCardOcrJob>()
            .HasOne(j => j.ScannedCardFile).WithMany()
            .HasForeignKey(j => j.ScannedCardFileId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BusinessCardOcrJob>()
            .HasOne(j => j.ApiConfiguration).WithMany()
            .HasForeignKey(j => j.ApiConfigId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BusinessCardOcrJob>()
            .HasOne(j => j.MatchedPartner).WithMany()
            .HasForeignKey(j => j.MatchedPartnerId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<BusinessCardOcrJob>()
            .HasOne(j => j.ConfirmedPartner).WithMany()
            .HasForeignKey(j => j.ConfirmedPartnerId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<BusinessCardOcrJob>()
            .HasOne(j => j.ConfirmedContact).WithMany()
            .HasForeignKey(j => j.ConfirmedContactId).OnDelete(DeleteBehavior.SetNull);

        // UploadedFile (uploaded_by → User, SET NULL)
        modelBuilder.Entity<UploadedFile>()
            .HasOne<User>().WithMany()
            .HasForeignKey(f => f.UploadedBy).OnDelete(DeleteBehavior.SetNull);

        // Document → UploadedFile
        modelBuilder.Entity<Document>()
            .HasOne(d => d.File).WithMany(f => f.Documents)
            .HasForeignKey(d => d.FileId).OnDelete(DeleteBehavior.Restrict);

        // VisitRequest → Partner, VisitorUser, CancelledBy
        // (decision fields moved down to VisitRequestCampus — SQL v10 campus-independent approval)
        modelBuilder.Entity<VisitRequest>()
            .HasOne(v => v.Partner).WithMany()
            .HasForeignKey(v => v.PartnerId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitRequest>()
            .HasOne<User>().WithMany()
            .HasForeignKey(v => v.VisitorUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<VisitRequest>()
            .HasOne<User>().WithMany()
            .HasForeignKey(v => v.CancelledBy).OnDelete(DeleteBehavior.SetNull);

        // VisitRequestCampus → VisitRequest, Campus, host/transfer/closed/cancelled users
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
            .HasForeignKey(vc => vc.HostAssignedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vc => vc.CoordinatorUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vc => vc.CoordinatorAssignedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vc => vc.DecidedBy).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vc => vc.ClosedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne<User>().WithMany()
            .HasForeignKey(vc => vc.CancelledBy).OnDelete(DeleteBehavior.SetNull);

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
        // FK to instance is ON DELETE CASCADE in SQL (visit_agendas.fk_visit_agendas_instance).
        // source_template_item_id / created_by / updated_by stay plain scalar columns (the DB owns
        // their FKs); no EF navigation is needed and adding one would only invent shadow state.
        modelBuilder.Entity<VisitAgenda>()
            .HasOne(va => va.VisitInstance).WithMany(vc => vc.Agendas)
            .HasForeignKey(va => va.VisitInstanceId).OnDelete(DeleteBehavior.Cascade);
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

        // VisitLogisticsItemHandover (v10) → LogisticsItem + signer users + attachment file
        modelBuilder.Entity<VisitLogisticsItemHandover>()
            .HasOne(h => h.LogisticsItem).WithMany(li => li.Handovers)
            .HasForeignKey(h => h.LogisticsItemId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VisitLogisticsItemHandover>()
            .HasIndex(h => new { h.LogisticsItemId, h.HandoverType })
            .IsUnique()
            .HasDatabaseName("uq_logistics_handover_type");
        modelBuilder.Entity<VisitLogisticsItemHandover>()
            .HasOne(h => h.BorrowerSignedByUser).WithMany()
            .HasForeignKey(h => h.BorrowerSignedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItemHandover>()
            .HasOne(h => h.ProviderSignedByUser).WithMany()
            .HasForeignKey(h => h.ProviderSignedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItemHandover>()
            .HasOne(h => h.AttachmentFile).WithMany()
            .HasForeignKey(h => h.AttachmentFileId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<VisitLogisticsItemHandover>()
            .HasOne(h => h.CreatedByUser).WithMany()
            .HasForeignKey(h => h.CreatedBy).OnDelete(DeleteBehavior.SetNull);

        // VisitLogisticsAssignmentAttempt (v10) → LogisticsItem, AssigneeUser, AssignedBy
        modelBuilder.Entity<VisitLogisticsAssignmentAttempt>()
            .HasOne(a => a.LogisticsItem).WithMany()
            .HasForeignKey(a => a.LogisticsItemId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<VisitLogisticsAssignmentAttempt>()
            .HasIndex(a => new { a.LogisticsItemId, a.Status })
            .HasDatabaseName("idx_vla_item_status");

        // VisitInstanceReminderSetting (v10 2026-06-26) → VisitRequestCampus + created/updated users.
        // Enum columns persist as their SQL ENUM string (name maps 1:1). Unique per
        // (instance, channel, target_group); index on (status, scheduled_at) drives the dispatch job.
        modelBuilder.Entity<VisitInstanceReminderSetting>(b =>
        {
            b.HasOne(r => r.VisitInstance).WithMany()
                .HasForeignKey(r => r.VisitInstanceId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<User>().WithMany()
                .HasForeignKey(r => r.CreatedBy).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<User>().WithMany()
                .HasForeignKey(r => r.UpdatedBy).OnDelete(DeleteBehavior.SetNull);
            b.Property(r => r.Channel).HasConversion<string>();
            b.Property(r => r.TargetGroup).HasConversion<string>();
            b.Property(r => r.Status).HasConversion<string>();
            b.HasIndex(r => new { r.VisitInstanceId, r.Channel, r.TargetGroup })
                .IsUnique().HasDatabaseName("uq_visit_reminder_channel_target");
            b.HasIndex(r => new { r.Status, r.ScheduledAt })
                .HasDatabaseName("idx_visit_reminder_schedule");
        });

        // Minute → VisitRequestCampus, CreatedBy, EditLockedBy
        modelBuilder.Entity<Minute>()
            .HasOne<VisitRequestCampus>().WithMany()
            .HasForeignKey(m => m.VisitInstanceId).OnDelete(DeleteBehavior.Restrict);
        // Exactly ONE minutes record per campus instance (see patch_minutes_unique_visit_instance.sql
        // for the matching DB UNIQUE KEY; the create handler also re-checks inside a transaction).
        modelBuilder.Entity<Minute>()
            .HasIndex(m => m.VisitInstanceId).IsUnique();
        modelBuilder.Entity<Minute>()
            .HasOne<User>().WithMany()
            .HasForeignKey(m => m.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Minute>()
            .HasOne<User>().WithMany()
            .HasForeignKey(m => m.EditLockedBy).OnDelete(DeleteBehavior.SetNull);

        // Feedback (v10 feedback_rule_fixed) → VisitRequest, VisitRequestCampus, SubmittedBy and the
        // typed business targets. All FKs are RESTRICT per the SQL schema; the type/target flow rule
        // (chk_feedbacks_flow) plus target-column mapping is validated in FeedbackRules (backend).
        modelBuilder.Entity<Feedback>()
            .HasOne<VisitRequest>().WithMany()
            .HasForeignKey(f => f.VisitRequestId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<VisitRequestCampus>().WithMany()
            .HasForeignKey(f => f.VisitInstanceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<User>().WithMany()
            .HasForeignKey(f => f.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<User>().WithMany()
            .HasForeignKey(f => f.TargetUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<VisitParticipant>().WithMany()
            .HasForeignKey(f => f.TargetParticipantId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<VisitGuestMember>().WithMany()
            .HasForeignKey(f => f.TargetGuestMemberId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<VisitLogisticsItem>().WithMany()
            .HasForeignKey(f => f.TargetLogisticsItemId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<VisitLogisticsItemHandover>().WithMany()
            .HasForeignKey(f => f.TargetHandoverId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Feedback>()
            .HasOne<Department>().WithMany()
            .HasForeignKey(f => f.TargetDepartmentId).OnDelete(DeleteBehavior.Restrict);

        // News → Campus, VisitInstance, AuthorUser, CoverFile, ReviewedBy
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<Campus>().WithMany()
            .HasForeignKey(n => n.CampusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<VisitRequestCampus>().WithMany()
            .HasForeignKey(n => n.VisitInstanceId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<User>().WithMany()
            .HasForeignKey(n => n.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<UploadedFile>().WithMany()
            .HasForeignKey(n => n.CoverFileId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PEMS.Domain.Entities.News.News>()
            .HasOne<User>().WithMany()
            .HasForeignKey(n => n.ReviewedBy).OnDelete(DeleteBehavior.SetNull);

        // NewsTranslation → News
        modelBuilder.Entity<NewsTranslation>()
            .HasOne(nt => nt.News).WithMany(n => n.Translations)
            .HasForeignKey(nt => nt.NewsId).OnDelete(DeleteBehavior.Cascade);

        // GalleryArea → Campus, CreatedBy, UpdatedBy
        modelBuilder.Entity<GalleryArea>()
            .HasOne(a => a.Campus).WithMany()
            .HasForeignKey(a => a.CampusId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GalleryArea>()
            .HasOne<User>().WithMany()
            .HasForeignKey(a => a.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryArea>()
            .HasOne<User>().WithMany()
            .HasForeignKey(a => a.UpdatedBy).OnDelete(DeleteBehavior.SetNull);

        // GalleryLocation → Area, CreatedBy, UpdatedBy
        modelBuilder.Entity<GalleryLocation>()
            .HasOne(l => l.Area).WithMany(a => a.Locations)
            .HasForeignKey(l => l.AreaId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GalleryLocation>()
            .HasOne<User>().WithMany()
            .HasForeignKey(l => l.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryLocation>()
            .HasOne<User>().WithMany()
            .HasForeignKey(l => l.UpdatedBy).OnDelete(DeleteBehavior.SetNull);

        // GalleryItem → Location, CreatedBy, UpdatedBy, DeletedBy
        modelBuilder.Entity<GalleryItem>()
            .HasOne(i => i.Location).WithMany(l => l.Items)
            .HasForeignKey(i => i.LocationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GalleryItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(i => i.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(i => i.UpdatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryItem>()
            .HasOne<User>().WithMany()
            .HasForeignKey(i => i.DeletedBy).OnDelete(DeleteBehavior.SetNull);

        // GalleryItemMedia → GalleryItem, File, ThumbnailFile, CreatedBy, UpdatedBy, DeletedBy
        modelBuilder.Entity<GalleryItemMedia>()
            .HasOne(m => m.GalleryItem).WithMany(i => i.Media)
            .HasForeignKey(m => m.GalleryItemId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GalleryItemMedia>()
            .HasOne(m => m.File).WithMany()
            .HasForeignKey(m => m.FileId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GalleryItemMedia>()
            .HasOne(m => m.ThumbnailFile).WithMany()
            .HasForeignKey(m => m.ThumbnailFileId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryItemMedia>()
            .HasOne<User>().WithMany()
            .HasForeignKey(m => m.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryItemMedia>()
            .HasOne<User>().WithMany()
            .HasForeignKey(m => m.UpdatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryItemMedia>()
            .HasOne<User>().WithMany()
            .HasForeignKey(m => m.DeletedBy).OnDelete(DeleteBehavior.SetNull);

        // GalleryItemTtsAudio → GalleryItem, CreatedBy, UpdatedBy. audio_file_id stays a scalar (like
        // the gallery cover_file_id columns) — the DB owns that FK. The DB's generated running_key
        // column is not mapped at all, so EF never tries to write it.
        modelBuilder.Entity<GalleryItemTtsAudio>()
            .HasOne(t => t.GalleryItem).WithMany()
            .HasForeignKey(t => t.GalleryItemId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<GalleryItemTtsAudio>()
            .HasOne<User>().WithMany()
            .HasForeignKey(t => t.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryItemTtsAudio>()
            .HasOne<User>().WithMany()
            .HasForeignKey(t => t.UpdatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<GalleryItemTtsAudio>()
            .Property(t => t.SpeedRate).HasPrecision(3, 1);
        modelBuilder.Entity<GalleryItemTtsAudio>()
            .Property(t => t.PitchRate).HasPrecision(3, 1);
        modelBuilder.Entity<GalleryItemTtsAudio>()
            .Property(t => t.Progress).HasPrecision(5, 2);

        // PhotoFaceTag → File, TaggedUser, VisitRequest, GuestMember, PartnerContact, CreatedBy, RemovedBy
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.File).WithMany()
            .HasForeignKey(ft => ft.FileId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.TaggedUser).WithMany()
            .HasForeignKey(ft => ft.TaggedUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.VisitRequest).WithMany()
            .HasForeignKey(ft => ft.VisitRequestId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.GuestMember).WithMany()
            .HasForeignKey(ft => ft.GuestMemberId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.PartnerContact).WithMany()
            .HasForeignKey(ft => ft.PartnerContactId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne<User>().WithMany()
            .HasForeignKey(ft => ft.CreatedBy).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne<User>().WithMany()
            .HasForeignKey(ft => ft.RemovedBy).OnDelete(DeleteBehavior.SetNull);

        // SentEmail → EmailTemplate, SentBy
        modelBuilder.Entity<SentEmail>()
            .HasOne(se => se.EmailTemplate).WithMany()
            .HasForeignKey(se => se.EmailTemplateId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SentEmail>()
            .HasOne<User>().WithMany()
            .HasForeignKey(se => se.SentBy).OnDelete(DeleteBehavior.SetNull);

        // ── Email rich-text / attachments / drafts (v10 email_rich_editor) ────
        // body_format ENUM persists as its SQL string (PLAIN_TEXT / HTML); name maps 1:1.
        modelBuilder.Entity<EmailTemplate>()
            .Property(t => t.BodyFormat).HasConversion<string>();
        modelBuilder.Entity<SentEmail>()
            .Property(se => se.BodyFormat).HasConversion<string>();

        // sent_email_attachments → sent_emails (CASCADE), files (RESTRICT)
        modelBuilder.Entity<SentEmailAttachment>(b =>
        {
            b.Property(a => a.AttachmentType).HasConversion<string>();
            b.HasOne(a => a.SentEmail).WithMany(e => e.Attachments)
                .HasForeignKey(a => a.SentEmailId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.File).WithMany()
                .HasForeignKey(a => a.FileId).OnDelete(DeleteBehavior.Restrict);
            // SQL: UNIQUE KEY uq_sent_email_attachments_content (sent_email_id, content_id)
            b.HasIndex(a => new { a.SentEmailId, a.ContentId })
                .IsUnique().HasDatabaseName("uq_sent_email_attachments_content");
            b.HasIndex(a => new { a.SentEmailId, a.AttachmentType })
                .HasDatabaseName("idx_sent_email_attachments_type");
        });

        // email_drafts → template (SET NULL), sent_email (SET NULL), created_by/last_edited_by (SET NULL)
        modelBuilder.Entity<EmailDraft>(b =>
        {
            b.Property(d => d.BodyFormat).HasConversion<string>();
            b.Property(d => d.Status).HasConversion<string>();
            b.HasOne(d => d.EmailTemplate).WithMany()
                .HasForeignKey(d => d.EmailTemplateId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(d => d.SentEmail).WithMany()
                .HasForeignKey(d => d.SentEmailId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<User>().WithMany()
                .HasForeignKey(d => d.CreatedBy).OnDelete(DeleteBehavior.SetNull);
            b.HasOne<User>().WithMany()
                .HasForeignKey(d => d.LastEditedBy).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(d => new { d.Status, d.UpdatedAt }).HasDatabaseName("idx_email_drafts_status_updated");
            b.HasIndex(d => new { d.CreatedBy, d.Status }).HasDatabaseName("idx_email_drafts_created_by_status");
            b.HasIndex(d => new { d.RelatedType, d.RelatedId }).HasDatabaseName("idx_email_drafts_related");
        });

        // email_draft_recipients → email_drafts (CASCADE)
        modelBuilder.Entity<EmailDraftRecipient>(b =>
        {
            b.HasOne(r => r.EmailDraft).WithMany(d => d.Recipients)
                .HasForeignKey(r => r.EmailDraftId).OnDelete(DeleteBehavior.Cascade);
            // SQL: UNIQUE KEY uq_email_draft_recipients_unique (email_draft_id, recipient_email, recipient_type)
            b.HasIndex(r => new { r.EmailDraftId, r.RecipientEmail, r.RecipientType })
                .IsUnique().HasDatabaseName("uq_email_draft_recipients_unique");
        });

        // email_draft_attachments → email_drafts (CASCADE), files (RESTRICT)
        modelBuilder.Entity<EmailDraftAttachment>(b =>
        {
            b.Property(a => a.AttachmentType).HasConversion<string>();
            b.HasOne(a => a.EmailDraft).WithMany(d => d.Attachments)
                .HasForeignKey(a => a.EmailDraftId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(a => a.File).WithMany()
                .HasForeignKey(a => a.FileId).OnDelete(DeleteBehavior.Restrict);
            // SQL: UNIQUE KEY uq_email_draft_attachments_content (email_draft_id, content_id)
            b.HasIndex(a => new { a.EmailDraftId, a.ContentId })
                .IsUnique().HasDatabaseName("uq_email_draft_attachments_content");
            b.HasIndex(a => new { a.EmailDraftId, a.AttachmentType })
                .HasDatabaseName("idx_email_draft_attachments_type");
        });

        // EmailActionToken (v10) → RecipientUser, SentEmail, SentEmailRecipient
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.RecipientUser).WithMany()
            .HasForeignKey(t => t.RecipientUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.SentEmail).WithMany()
            .HasForeignKey(t => t.SentEmailId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.SentEmailRecipient).WithMany()
            .HasForeignKey(t => t.SentEmailRecipientId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<EmailActionToken>()
            .HasIndex(t => t.TokenHash).IsUnique()
            .HasDatabaseName("uq_email_action_token_hash");

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

        // ── JSON columns ──────────────────────────────────────────────────
        // (Removed in v8.4 patch)


        // ── Remaining child relationships ─────────────────────────────────
        modelBuilder.Entity<MinuteActionItem>()
            .HasOne(a => a.Minute).WithMany(m => m.ActionItems)
            .HasForeignKey(a => a.MinutesId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(a => a.Minute).WithMany(m => m.Participants)
            .HasForeignKey(a => a.MinutesId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(a => a.User).WithMany()
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(a => a.GuestMember).WithMany()
            .HasForeignKey(a => a.GuestMemberId).OnDelete(DeleteBehavior.SetNull);
        // Map the second User navigation (attendance checker) onto the real checked_by column;
        // without this EF convention would invent a shadow FK column that does not exist in the DB.
        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(a => a.CheckedByUser).WithMany()
            .HasForeignKey(a => a.CheckedBy).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<FeedbackRatingItem>()
            .HasOne(a => a.Feedback).WithMany(f => f.RatingItems)
            .HasForeignKey(a => a.FeedbackId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SentEmailRecipient>()
            .HasOne(a => a.SentEmail).WithMany(e => e.Recipients)
            .HasForeignKey(a => a.SentEmailId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CalendarEventAttendee>()
            .HasOne(a => a.CalendarEvent).WithMany(e => e.Attendees)
            .HasForeignKey(a => a.CalendarEventId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CalendarEventAttendee>()
            .HasOne(a => a.User).WithMany()
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CalendarEventReminder>()
            .HasOne(a => a.CalendarEvent).WithMany(e => e.Reminders)
            .HasForeignKey(a => a.CalendarEventId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ApiConfigurationHeader>()
            .HasOne(a => a.ApiConfiguration).WithMany(c => c.Headers)
            .HasForeignKey(a => a.ApiConfigId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AgendaTemplateItem>()
            .HasOne(a => a.AgendaTemplate).WithMany(t => t.Items)
            .HasForeignKey(a => a.AgendaTemplateId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AuditLogChange>()
            .HasOne(a => a.AuditLog).WithMany(l => l.Changes)
            .HasForeignKey(a => a.AuditLogId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NewsContentSection>()
            .HasOne(s => s.NewsTranslation).WithMany(t => t.Sections)
            .HasForeignKey(s => s.NewsTranslationId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NewsSectionFile>(b =>
        {
            b.HasOne(f => f.Section).WithMany(s => s.SectionFiles).HasForeignKey(f => f.SectionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(f => f.File).WithMany().HasForeignKey(f => f.FileId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
