using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Constants;
using PEMS.Shared;
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

namespace PEMS.UnitTests.TestInfrastructure;

/// <summary>
/// EF Core InMemory stand-in for <see cref="IApplicationDbContext"/> covering the host-preparation
/// Delegations slice (support departments, participant candidates, invite participant, prepare
/// logistics). Same construction rules as <see cref="TestApplicationDbContext"/> (UC-106): only the
/// aggregates these handlers touch are PUBLIC DbSet properties; everything else is an explicit
/// interface member EF never discovers, and reachable-but-unused aggregates are Ignored so model
/// finalization succeeds. InMemory has no real transactions (warning suppressed).
/// </summary>
public class DelegationsTestDbContext : DbContext, IApplicationDbContext
{
    public static DelegationsTestDbContext Create() =>
        new(new DbContextOptionsBuilder<DelegationsTestDbContext>()
            .UseInMemoryDatabase($"pems-delegations-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public DelegationsTestDbContext(DbContextOptions<DelegationsTestDbContext> options) : base(options) { }

    // ── Mapped aggregates (the four handlers + ScheduleConflictResolver) ──────
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<VisitRequest> VisitRequests => Set<VisitRequest>();
    public DbSet<VisitRequestCampus> VisitRequestCampuses => Set<VisitRequestCampus>();
    public DbSet<VisitParticipant> VisitParticipants => Set<VisitParticipant>();
    public DbSet<VisitLogisticsItem> VisitLogisticsItems => Set<VisitLogisticsItem>();
    // Student visit photo storage slice (upload/list/remove handlers + folder service).
    public DbSet<VisitPhotoFolder> VisitPhotoFolders => Set<VisitPhotoFolder>();
    public DbSet<VisitPhoto> VisitPhotos => Set<VisitPhoto>();
    public DbSet<UploadedFile> Files => Set<UploadedFile>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
    public DbSet<SentEmailRecipient> SentEmailRecipients => Set<SentEmailRecipient>();
    public DbSet<EmailActionToken> EmailActionTokens => Set<EmailActionToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Prune aggregates reachable from the mapped slice but never used by these handlers.
        modelBuilder.Ignore<UserAuthProvider>();
        modelBuilder.Ignore<UserSession>();
        modelBuilder.Ignore<Partner>();
        modelBuilder.Ignore<PartnerContact>();
        modelBuilder.Ignore<PartnerAlias>();
        modelBuilder.Ignore<VisitGuestPartnerLink>();
        modelBuilder.Ignore<VisitGuestMember>();
        modelBuilder.Ignore<VisitInstanceFormDetail>();
        modelBuilder.Ignore<VisitInstanceGuestMember>();
        modelBuilder.Ignore<VisitRequestIdentityChange>();
        modelBuilder.Ignore<VisitRequestIdentityChangeEvent>();
        modelBuilder.Ignore<VisitInstanceAmendment>();
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        modelBuilder.Ignore<VisitAgenda>();
        modelBuilder.Ignore<VisitLogisticsItemHandover>();
        modelBuilder.Ignore<VisitLogisticsAssignmentAttempt>();
        modelBuilder.Ignore<VisitInstanceReminderSetting>();
        modelBuilder.Ignore<CalendarEventAttendee>();
        modelBuilder.Ignore<CalendarEventReminder>();
        modelBuilder.Ignore<SentEmailAttachment>();
        modelBuilder.Ignore<AuditLogChange>();
        modelBuilder.Ignore<OtpToken>();
        modelBuilder.Ignore<LoginLog>();
        modelBuilder.Ignore<SecurityEvent>();
        modelBuilder.Ignore<Document>();
        modelBuilder.Ignore<Minute>();
        modelBuilder.Ignore<MinuteActionItem>();
        modelBuilder.Ignore<MinuteParticipant>();
        modelBuilder.Ignore<Feedback>();
        modelBuilder.Ignore<News>();
        modelBuilder.Ignore<NewsTranslation>();
        modelBuilder.Ignore<NewsContentSection>();
        modelBuilder.Ignore<NewsSectionFile>();
        modelBuilder.Ignore<Faq>();
        modelBuilder.Ignore<GalleryArea>();
        modelBuilder.Ignore<GalleryLocation>();
        modelBuilder.Ignore<GalleryItem>();
        modelBuilder.Ignore<GalleryItemMedia>();
        modelBuilder.Ignore<GalleryItemContent>();
        modelBuilder.Ignore<VisitExpenseReport>();
        modelBuilder.Ignore<VisitExpenseItem>();
        modelBuilder.Ignore<VisitExpenseReportEvent>();
        modelBuilder.Ignore<PhotoFaceTag>();
        modelBuilder.Ignore<EmailDraft>();
        modelBuilder.Ignore<EmailDraftRecipient>();
        modelBuilder.Ignore<EmailDraftAttachment>();
        modelBuilder.Ignore<Notification>();
        modelBuilder.Ignore<ApiConfiguration>();
        modelBuilder.Ignore<ApiUsageQuota>();
        modelBuilder.Ignore<ApiRequestLog>();
        modelBuilder.Ignore<BusinessCardOcrJob>();
        modelBuilder.Ignore<AgendaTemplate>();
        modelBuilder.Ignore<AgendaTemplateItem>();
        modelBuilder.Ignore<AgendaTemplateDefault>();

        // Same pairings as the production ApplicationDbContext (ambiguous by convention).
        modelBuilder.Entity<Campus>()
            .HasOne(c => c.IcHeadUser).WithMany().HasForeignKey(c => c.IcHeadUserId);
        modelBuilder.Entity<Department>()
            .HasOne(d => d.Campus).WithMany(c => c.Departments).HasForeignKey(d => d.CampusId);
        modelBuilder.Entity<Department>()
            .HasOne(d => d.HeadUser).WithMany().HasForeignKey(d => d.HeadUserId);
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId);
        modelBuilder.Entity<User>()
            .HasOne(u => u.PrimaryCampus).WithMany(c => c.Users).HasForeignKey(u => u.PrimaryCampusId);
        modelBuilder.Entity<User>()
            .HasOne(u => u.Department).WithMany(d => d.Users).HasForeignKey(u => u.DepartmentId);
        modelBuilder.Entity<VisitRequestCampus>()
            .HasOne(c => c.VisitRequest).WithMany(v => v.CampusInstances)
            .HasForeignKey(c => c.VisitRequestId);
        modelBuilder.Entity<VisitParticipant>()
            .HasOne(vp => vp.VisitInstance).WithMany(vc => vc.Participants)
            .HasForeignKey(vp => vp.VisitInstanceId);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne(li => li.VisitInstance).WithMany(vc => vc.LogisticsItems)
            .HasForeignKey(li => li.VisitInstanceId);
        modelBuilder.Entity<SentEmail>()
            .HasOne(s => s.EmailTemplate).WithMany().HasForeignKey(s => s.EmailTemplateId);
        modelBuilder.Entity<SentEmailRecipient>()
            .HasOne(r => r.SentEmail).WithMany(s => s.Recipients).HasForeignKey(r => r.SentEmailId);
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.RecipientUser).WithMany().HasForeignKey(t => t.RecipientUserId);
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.SentEmail).WithMany().HasForeignKey(t => t.SentEmailId);
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.SentEmailRecipient).WithMany().HasForeignKey(t => t.SentEmailRecipientId);
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.ActorUser).WithMany().HasForeignKey(a => a.ActorUserId);

        // NOTE: the DB unique keys of the visit-photo slice (one folder per request, one photo per
        // files row) are NOT modeled here — the InMemory provider only enforces primary/alternate
        // KEYS, not unique indexes. Tests that need the duplicate-key failure path simulate the
        // DbUpdateException deterministically via a SaveChanges-failing subclass.
    }

    // ── Aggregates this slice never touches (NOT discovered by EF) ────────────
    DbSet<UserAuthProvider> IApplicationDbContext.UserAuthProviders => Set<UserAuthProvider>();
    DbSet<UserSession> IApplicationDbContext.UserSessions => Set<UserSession>();
    DbSet<OtpToken> IApplicationDbContext.OtpTokens => Set<OtpToken>();
    DbSet<LoginLog> IApplicationDbContext.LoginLogs => Set<LoginLog>();
    DbSet<SecurityEvent> IApplicationDbContext.SecurityEvents => Set<SecurityEvent>();
    DbSet<Partner> IApplicationDbContext.Partners => Set<Partner>();
    DbSet<PartnerContact> IApplicationDbContext.PartnerContacts => Set<PartnerContact>();
    DbSet<PartnerAlias> IApplicationDbContext.PartnerAliases => Set<PartnerAlias>();
    DbSet<VisitGuestPartnerLink> IApplicationDbContext.VisitGuestPartnerLinks => Set<VisitGuestPartnerLink>();
    DbSet<Document> IApplicationDbContext.Documents => Set<Document>();
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
    DbSet<VisitAgenda> IApplicationDbContext.VisitAgendas => Set<VisitAgenda>();
    DbSet<VisitLogisticsItemHandover> IApplicationDbContext.VisitLogisticsItemHandovers => Set<VisitLogisticsItemHandover>();
    DbSet<VisitLogisticsAssignmentAttempt> IApplicationDbContext.VisitLogisticsAssignmentAttempts => Set<VisitLogisticsAssignmentAttempt>();
    DbSet<VisitInstanceReminderSetting> IApplicationDbContext.VisitInstanceReminderSettings => Set<VisitInstanceReminderSetting>();
    DbSet<VisitExpenseReport> IApplicationDbContext.VisitExpenseReports => Set<VisitExpenseReport>();
    DbSet<VisitExpenseItem> IApplicationDbContext.VisitExpenseItems => Set<VisitExpenseItem>();
    DbSet<VisitExpenseReportEvent> IApplicationDbContext.VisitExpenseReportEvents => Set<VisitExpenseReportEvent>();
    DbSet<Minute> IApplicationDbContext.Minutes => Set<Minute>();
    DbSet<MinuteActionItem> IApplicationDbContext.MinuteActionItems => Set<MinuteActionItem>();
    DbSet<MinuteParticipant> IApplicationDbContext.MinuteParticipants => Set<MinuteParticipant>();
    DbSet<Feedback> IApplicationDbContext.Feedbacks => Set<Feedback>();
    DbSet<News> IApplicationDbContext.News => Set<News>();
    DbSet<NewsTranslation> IApplicationDbContext.NewsTranslations => Set<NewsTranslation>();
    DbSet<NewsContentSection> IApplicationDbContext.NewsContentSections => Set<NewsContentSection>();
    DbSet<NewsSectionFile> IApplicationDbContext.NewsSectionFiles => Set<NewsSectionFile>();
    DbSet<Faq> IApplicationDbContext.Faqs => Set<Faq>();
    DbSet<GalleryArea> IApplicationDbContext.GalleryAreas => Set<GalleryArea>();
    DbSet<GalleryLocation> IApplicationDbContext.GalleryLocations => Set<GalleryLocation>();
    DbSet<GalleryItem> IApplicationDbContext.GalleryItems => Set<GalleryItem>();
    DbSet<GalleryItemMedia> IApplicationDbContext.GalleryItemMedia => Set<GalleryItemMedia>();
    DbSet<GalleryItemContent> IApplicationDbContext.GalleryItemContents => Set<GalleryItemContent>();
    DbSet<PhotoFaceTag> IApplicationDbContext.PhotoFaceTags => Set<PhotoFaceTag>();
    DbSet<SentEmailAttachment> IApplicationDbContext.SentEmailAttachments => Set<SentEmailAttachment>();
    DbSet<EmailDraft> IApplicationDbContext.EmailDrafts => Set<EmailDraft>();
    DbSet<EmailDraftRecipient> IApplicationDbContext.EmailDraftRecipients => Set<EmailDraftRecipient>();
    DbSet<EmailDraftAttachment> IApplicationDbContext.EmailDraftAttachments => Set<EmailDraftAttachment>();
    DbSet<Notification> IApplicationDbContext.Notifications => Set<Notification>();
    DbSet<CalendarEvent> IApplicationDbContext.CalendarEvents => CalendarEvents;
    DbSet<ApiConfiguration> IApplicationDbContext.ApiConfigurations => Set<ApiConfiguration>();
    DbSet<ApiUsageQuota> IApplicationDbContext.ApiUsageQuotas => Set<ApiUsageQuota>();
    DbSet<ApiRequestLog> IApplicationDbContext.ApiRequestLogs => Set<ApiRequestLog>();
    DbSet<BusinessCardOcrJob> IApplicationDbContext.BusinessCardOcrJobs => Set<BusinessCardOcrJob>();
    DbSet<AgendaTemplate> IApplicationDbContext.AgendaTemplates => Set<AgendaTemplate>();
    DbSet<AgendaTemplateItem> IApplicationDbContext.AgendaTemplateItems => Set<AgendaTemplateItem>();
    DbSet<AgendaTemplateDefault> IApplicationDbContext.AgendaTemplateDefaults => Set<AgendaTemplateDefault>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);
}

/// <summary>
/// Fixture builders for the host-preparation Delegations tests. One campus (id 1) with a host
/// (STAFF+STAFF, id 100) assigned to instance 10 of visit request 10; ids of extra rows are chosen
/// by each test so assertions stay explicit.
/// </summary>
public static class DelegationsTestData
{
    public const ulong CampusId = 1;
    public const ulong OtherCampusId = 2;
    public const ulong StaffRoleId = 3;
    public const ulong DepartmentRoleId = 4;
    public const ulong StudentRoleId = 5;

    public const ulong HostUserId = 100;
    public const ulong VisitInstanceId = 10;
    public const ulong VisitRequestId = 10;

    public static Campus CreateCampus(ulong campusId = CampusId) => new()
    {
        CampusId = campusId,
        CampusCode = $"C{campusId}",
        Name = $"Campus {campusId}",
        Status = EntityStatuses.Active,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static Role CreateRole(ulong roleId, string roleCode) => new()
    {
        RoleId = roleId,
        RoleCode = roleCode,
        Name = roleCode,
        Status = EntityStatuses.Active,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static Department CreateDepartment(
        ulong departmentId, string departmentType = "GENERAL", ulong campusId = CampusId,
        string status = EntityStatuses.Active, ulong? headUserId = null) => new()
    {
        DepartmentId = departmentId,
        CampusId = campusId,
        Name = $"Phòng ban {departmentId}",
        DepartmentType = departmentType,
        Status = status,
        HeadUserId = headUserId,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static User CreateUser(
        ulong userId, ulong roleId, string? subRole, ulong? departmentId,
        ulong campusId = CampusId, string status = "ACTIVE") => new()
    {
        UserId = userId,
        FullName = $"User {userId}",
        Email = $"user{userId}@test.local",
        RoleId = roleId,
        SubRole = subRole,
        PrimaryCampusId = campusId,
        DepartmentId = departmentId,
        Status = status,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static VisitRequest CreateVisitRequest(ulong visitRequestId = VisitRequestId) => new()
    {
        VisitRequestId = visitRequestId,
        RequestCode = $"VR-{visitRequestId}",
        RegistrantFullName = "Nguyễn Văn Khách",
        RegistrantNationality = "VN",
        RegistrantOrganization = "Đối tác",
        RegistrantJobTitle = "Trưởng đoàn",
        RegistrantPhone = "0900000000",
        RegistrantEmail = "guest@test.local",
        DelegationName = "Đoàn khách kiểm thử",
        Purpose = "Tham quan",
        ContactPersonFullName = "Đầu mối",
        ContactPersonOrganization = "Đối tác",
        ContactPersonPhone = "0900000001",
        ContactPersonEmail = "contact@test.local",
        Status = "APPROVED",
        SubmittedAt = new DateTime(2026, 6, 1),
        CreatedAt = new DateTime(2026, 6, 1),
    };

    public static VisitRequestCampus CreateVisitInstance(
        ulong visitInstanceId = VisitInstanceId,
        string status = VisitInstanceStatus.BeforeVisit,
        ulong campusId = CampusId,
        ulong? currentHostUserId = HostUserId,
        ulong visitRequestId = VisitRequestId) => new()
    {
        VisitInstanceId = visitInstanceId,
        VisitRequestId = visitRequestId,
        CampusId = campusId,
        PlannedStartAt = new DateTime(2026, 8, 1, 9, 0, 0),
        PlannedEndAt = new DateTime(2026, 8, 1, 11, 0, 0),
        Status = status,
        CurrentHostUserId = currentHostUserId,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    public static VisitParticipant CreateParticipant(
        ulong participantId, ulong userId, string participantRole, string status,
        ulong visitInstanceId = VisitInstanceId) => new()
    {
        ParticipantId = participantId,
        VisitInstanceId = visitInstanceId,
        UserId = userId,
        ParticipantRole = participantRole,
        Status = status,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    /// <summary>Seeds the shared skeleton: both campuses, the 3 roles, the host (STAFF+STAFF of an
    /// ACTIVE IC department on campus 1) and the visit request + instance hosted by them.</summary>
    public static (VisitRequestCampus Instance, User Host) SeedBase(
        DelegationsTestDbContext db, string instanceStatus = VisitInstanceStatus.BeforeVisit)
    {
        db.Campuses.AddRange(CreateCampus(CampusId), CreateCampus(OtherCampusId));
        db.Roles.AddRange(
            CreateRole(StaffRoleId, RoleCodes.Staff),
            CreateRole(DepartmentRoleId, RoleCodes.Department),
            CreateRole(StudentRoleId, RoleCodes.Student));

        var icDept = CreateDepartment(900, departmentType: "IC");
        db.Departments.Add(icDept);

        var host = CreateUser(HostUserId, StaffRoleId, UserSubRoles.Staff, icDept.DepartmentId);
        db.Users.Add(host);

        db.VisitRequests.Add(CreateVisitRequest());
        var instance = CreateVisitInstance(status: instanceStatus);
        db.VisitRequestCampuses.Add(instance);
        db.SaveChanges();
        return (instance, host);
    }
}

/// <summary>Mutable current-user stub defaulting to the instance host of the Delegations fixtures.</summary>
public sealed class FakeDelegationsCurrentUser : ICurrentUserService
{
    public bool IsAuthenticated { get; set; } = true;
    public ulong? UserId { get; set; } = DelegationsTestData.HostUserId;
    public string? Email { get; set; } = "host@test.local";
    public ulong? RoleId { get; set; } = DelegationsTestData.StaffRoleId;
    public string? RoleCode { get; set; } = RoleCodes.Staff;
    public string? SubRole { get; set; } = UserSubRoles.Staff;
    public ulong? PrimaryCampusId { get; set; } = DelegationsTestData.CampusId;
    public ulong? DepartmentId { get; set; }
    public ulong? SessionId { get; set; } = 1;
    public string? LoginPortal { get; set; } = "INTERNAL";
}

/// <summary>Loose mocks for the outbound side of the invite/logistics handlers. The email service
/// records every send; tests flip <see cref="FailEmail"/> to prove commit-before-email.</summary>
public sealed class DelegationsHandlerMocks
{
    public Mock<IEmailService> Email { get; } = new(MockBehavior.Loose);
    public Mock<IEmailActionTokenService> Tokens { get; } = new(MockBehavior.Loose);
    public Mock<IHtmlSanitizerService> Sanitizer { get; } = new(MockBehavior.Loose);
    public Mock<IFileStorageService> Storage { get; } = new(MockBehavior.Loose);
    public Mock<PEMS.Application.Emails.Utils.IEmailImageLayoutNormalizer> Normalizer { get; } = new(MockBehavior.Loose);
    public Mock<PEMS.Application.Notifications.Common.INotificationService> Notifications { get; } = new(MockBehavior.Loose);
    public FakeDateTimeService Clock { get; } = new();

    public List<OutboundEmail> SentEmails { get; } = new();
    public bool FailEmail { get; set; }

    public DelegationsHandlerMocks()
    {
        var seq = 0;
        Tokens.Setup(t => t.GenerateRawToken()).Returns(() => $"raw-token-{++seq}");
        Tokens.Setup(t => t.Hash(It.IsAny<string>())).Returns<string>(raw => $"hash({raw})");
        Tokens.Setup(t => t.BuildPublicActionUrl(It.IsAny<string>())).Returns<string>(raw => $"https://pems.test/email-actions/{raw}");
        Tokens.Setup(t => t.BuildDepartmentAssignmentUrl(It.IsAny<ulong>(), It.IsAny<ulong>()))
            .Returns((ulong i, ulong p) => $"https://pems.test/departments/assign/{i}/{p}");
        Tokens.Setup(t => t.BuildLogisticsDetailUrl(It.IsAny<ulong>()))
            .Returns((ulong id) => $"https://pems.test/logistics/{id}");

        Normalizer.Setup(n => n.NormalizeHtmlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string html, CancellationToken _) => html);
        Sanitizer.Setup(s => s.SanitizeEmailHtml(It.IsAny<string>())).Returns<string>(h => h);

        Email.Setup(e => e.SendAsync(It.IsAny<OutboundEmail>(), It.IsAny<CancellationToken>()))
            .Returns((OutboundEmail mail, CancellationToken _) =>
            {
                if (FailEmail) throw new InvalidOperationException("SMTP down (test)");
                SentEmails.Add(mail);
                return Task.CompletedTask;
            });
    }
}
