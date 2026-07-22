using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;
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
/// EF Core InMemory stand-in for <see cref="IApplicationDbContext"/> used by handler unit tests
/// (no MySQL, no HTTP, no WebApplicationFactory). Only the aggregates the UC-106 handler touches
/// are declared as PUBLIC DbSet properties (EF's model discovery picks those up); every other
/// interface member is an explicit interface implementation, which EF does NOT discover, so the
/// unrelated half of the domain never enters the model — accessing one of those sets would throw,
/// which is exactly what we want. Relationship pairs that are ambiguous by convention (multiple
/// User FKs) mirror the fluent config of the production ApplicationDbContext. InMemory has no
/// real transactions, so <see cref="BeginTransactionAsync"/> returns the provider's no-op
/// transaction (TransactionIgnoredWarning suppressed).
/// </summary>
public sealed class TestApplicationDbContext : DbContext, IApplicationDbContext
{
    public static TestApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase($"pems-uc106-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public TestApplicationDbContext(DbContextOptions<TestApplicationDbContext> options) : base(options) { }

    // ── Mapped aggregates (UC-106 handler + impact calculator) ───────────────
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserAuthProvider> UserAuthProviders => Set<UserAuthProvider>();
    public DbSet<VisitRequestCampus> VisitRequestCampuses => Set<VisitRequestCampus>();
    public DbSet<VisitParticipant> VisitParticipants => Set<VisitParticipant>();
    public DbSet<VisitLogisticsItem> VisitLogisticsItems => Set<VisitLogisticsItem>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Prune everything outside the UC-106 slice. EF discovers even explicit-interface DbSet
        // properties, so each unrelated aggregate must be ignored explicitly or its (unconfigured)
        // relationships break model finalization.
        modelBuilder.Ignore<VisitRequest>();               // VisitRequestCampus.VisitRequest → Partner/guests
        modelBuilder.Ignore<VisitAgenda>();                // VisitRequestCampus.Agendas
        modelBuilder.Ignore<VisitLogisticsItemHandover>(); // VisitLogisticsItem.Handovers
        modelBuilder.Ignore<OtpToken>();
        modelBuilder.Ignore<LoginLog>();
        modelBuilder.Ignore<SecurityEvent>();
        modelBuilder.Ignore<Partner>();
        modelBuilder.Ignore<PartnerContact>();
        modelBuilder.Ignore<PartnerAlias>();
        modelBuilder.Ignore<VisitGuestPartnerLink>();
        modelBuilder.Ignore<UploadedFile>();
        modelBuilder.Ignore<Document>();
        modelBuilder.Ignore<VisitGuestMember>();
        modelBuilder.Ignore<VisitInstanceFormDetail>();
        modelBuilder.Ignore<VisitInstanceGuestMember>();
        modelBuilder.Ignore<VisitRequestIdentityChange>();
        modelBuilder.Ignore<VisitRequestIdentityChangeEvent>();
        modelBuilder.Ignore<VisitInstanceAmendment>();
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        modelBuilder.Ignore<VisitLogisticsAssignmentAttempt>();
        modelBuilder.Ignore<VisitInstanceReminderSetting>();
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
        modelBuilder.Ignore<EmailTemplate>();
        modelBuilder.Ignore<SentEmail>();
        modelBuilder.Ignore<SentEmailRecipient>();
        modelBuilder.Ignore<SentEmailAttachment>();
        modelBuilder.Ignore<EmailDraft>();
        modelBuilder.Ignore<EmailDraftRecipient>();
        modelBuilder.Ignore<EmailDraftAttachment>();
        modelBuilder.Ignore<EmailActionToken>();
        modelBuilder.Ignore<Notification>();
        modelBuilder.Ignore<CalendarEvent>();
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
        modelBuilder.Entity<UserSession>()
            .HasOne(s => s.User).WithMany(u => u.Sessions).HasForeignKey(s => s.UserId);
        // UserAuthProvider is mapped (HO basic-info email-change re-points providers). Configure both
        // navigations that reference it so model finalization succeeds.
        modelBuilder.Entity<UserAuthProvider>()
            .HasOne(p => p.User).WithMany(u => u.AuthProviders).HasForeignKey(p => p.UserId);
        modelBuilder.Entity<UserSession>()
            .HasOne(s => s.AuthProvider).WithMany().HasForeignKey(s => s.AuthProviderId);
        modelBuilder.Entity<VisitParticipant>()
            .HasOne(vp => vp.VisitInstance).WithMany(vc => vc.Participants)
            .HasForeignKey(vp => vp.VisitInstanceId);
        modelBuilder.Entity<VisitLogisticsItem>()
            .HasOne(li => li.VisitInstance).WithMany(vc => vc.LogisticsItems)
            .HasForeignKey(li => li.VisitInstanceId);
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.ActorUser).WithMany().HasForeignKey(a => a.ActorUserId);
    }

    // ── Aggregates the UC-106 slice never touches (NOT discovered by EF) ──────
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
    DbSet<VisitAgenda> IApplicationDbContext.VisitAgendas => Set<VisitAgenda>();
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
    DbSet<News> IApplicationDbContext.News => Set<News>();
    DbSet<NewsTranslation> IApplicationDbContext.NewsTranslations => Set<NewsTranslation>();
    DbSet<NewsContentSection> IApplicationDbContext.NewsContentSections => Set<NewsContentSection>();
    DbSet<NewsSectionFile> IApplicationDbContext.NewsSectionFiles => Set<NewsSectionFile>();
    DbSet<Faq> IApplicationDbContext.Faqs => Set<Faq>();
    DbSet<FaqTranslation> IApplicationDbContext.FaqTranslations => Set<FaqTranslation>();
    DbSet<GalleryArea> IApplicationDbContext.GalleryAreas => Set<GalleryArea>();
    DbSet<GalleryLocation> IApplicationDbContext.GalleryLocations => Set<GalleryLocation>();
    DbSet<GalleryItem> IApplicationDbContext.GalleryItems => Set<GalleryItem>();
    DbSet<GalleryItemMedia> IApplicationDbContext.GalleryItemMedia => Set<GalleryItemMedia>();
    DbSet<GalleryItemContent> IApplicationDbContext.GalleryItemContents => Set<GalleryItemContent>();
    DbSet<PhotoFaceTag> IApplicationDbContext.PhotoFaceTags => Set<PhotoFaceTag>();
    DbSet<EmailTemplate> IApplicationDbContext.EmailTemplates => Set<EmailTemplate>();
    DbSet<SentEmail> IApplicationDbContext.SentEmails => Set<SentEmail>();
    DbSet<SentEmailRecipient> IApplicationDbContext.SentEmailRecipients => Set<SentEmailRecipient>();
    DbSet<SentEmailAttachment> IApplicationDbContext.SentEmailAttachments => Set<SentEmailAttachment>();
    DbSet<EmailDraft> IApplicationDbContext.EmailDrafts => Set<EmailDraft>();
    DbSet<EmailDraftRecipient> IApplicationDbContext.EmailDraftRecipients => Set<EmailDraftRecipient>();
    DbSet<EmailDraftAttachment> IApplicationDbContext.EmailDraftAttachments => Set<EmailDraftAttachment>();
    DbSet<EmailActionToken> IApplicationDbContext.EmailActionTokens => Set<EmailActionToken>();
    DbSet<Notification> IApplicationDbContext.Notifications => Set<Notification>();
    DbSet<CalendarEvent> IApplicationDbContext.CalendarEvents => Set<CalendarEvent>();
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

/// <summary>Deterministic Staff Leader identity (mutable so tests can impersonate other actors).</summary>
public sealed class FakeCurrentUserService : ICurrentUserService
{
    public bool IsAuthenticated { get; set; } = true;
    public ulong? UserId { get; set; } = 900;
    public string? Email { get; set; } = "staff.leader@test.local";
    public ulong? RoleId { get; set; } = 3;
    public string? RoleCode { get; set; } = RoleCodes.Staff;
    public string? SubRole { get; set; } = UserSubRoles.Leader;
    public ulong? PrimaryCampusId { get; set; } = Uc106TestData.CampusId;
    public ulong? DepartmentId { get; set; }
    public ulong? SessionId { get; set; } = 1;
    public string? LoginPortal { get; set; } = "INTERNAL";
}

public sealed class FakeDateTimeService : IDateTimeService
{
    public DateTime UtcNow { get; set; } = new(2026, 7, 12, 8, 0, 0, DateTimeKind.Utc);
    public DateTime VietnamNow => UtcNow.AddHours(7);
}

/// <summary>
/// Records every <see cref="RevokeAllActiveSessionsAsync"/> call and revokes the matching
/// sessions in the shared test context (mirroring the real service, which writes through the
/// same scoped DbContext). All other members throw: UC-106 must never call them.
/// </summary>
public sealed class RecordingSessionService : ISessionService
{
    private readonly TestApplicationDbContext _db;

    public RecordingSessionService(TestApplicationDbContext db) => _db = db;

    public List<(ulong UserId, string Reason, ulong? RevokedBy)> RevokeAllCalls { get; } = new();

    public async Task<int> RevokeAllActiveSessionsAsync(
        ulong userId, string reason, ulong? revokedBy = null, CancellationToken cancellationToken = default)
    {
        RevokeAllCalls.Add((userId, reason, revokedBy));

        var now = DateTime.UtcNow;
        var active = await _db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var session in active)
        {
            session.RevokedAt = now;
            session.RevokedBy = revokedBy;
            session.RevokedReason = reason;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return active.Count;
    }

    public Task<SessionTokens> CreateSessionAsync(User user, string loginPortal, ulong? authProviderId,
        string? ipAddress, string? userAgent, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-106 must not create sessions.");

    public Task<UserSession?> GetActiveByRefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-106 must not read sessions by refresh token.");

    public Task<bool> IsSessionActiveAsync(ulong sessionId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-106 must not probe single sessions.");

    public Task<SessionTokens> RotateRefreshTokenAsync(UserSession session, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-106 must not rotate refresh tokens.");

    public Task RevokeSessionAsync(ulong sessionId, string reason, ulong? revokedBy = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-106 revokes per-user, not per-session.");
}

/// <summary>
/// Minimal-object builders for the UC-106 participant blocker fixtures (doc §22): department,
/// DEPARTMENT LEADER/STAFF users, visit instances and DEPT_SUPPORT participants. Ids are chosen
/// by the caller so assertions stay explicit.
/// </summary>
public static class Uc106TestData
{
    public const ulong CampusId = 1;
    public const ulong DepartmentRoleId = 4;
    public const ulong StaffRoleId = 3;
    public const ulong StudentRoleId = 5;

    public static Campus CreateCampus(ulong campusId = CampusId, string status = EntityStatuses.Active) => new()
    {
        CampusId = campusId,
        CampusCode = $"C{campusId}",
        Name = $"Campus {campusId}",
        Status = status,
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

    public static Department CreateGeneralDepartment(
        ulong departmentId, ulong campusId = CampusId, string status = EntityStatuses.Active) => new()
    {
        DepartmentId = departmentId,
        CampusId = campusId,
        Name = $"Phòng ban {departmentId}",
        DepartmentType = "GENERAL",
        Status = status,
        CreatedAt = new DateTime(2026, 1, 1),
        UpdatedAt = new DateTime(2026, 6, 1),
        UpdatedBy = 111,
    };

    public static User CreateDepartmentLeader(ulong userId, ulong departmentId, ulong campusId = CampusId)
        => CreateUser(userId, DepartmentRoleId, UserSubRoles.Leader, departmentId, campusId);

    public static User CreateDepartmentStaff(ulong userId, ulong departmentId, ulong campusId = CampusId)
        => CreateUser(userId, DepartmentRoleId, UserSubRoles.Staff, departmentId, campusId);

    public static User CreateUser(
        ulong userId, ulong roleId, string? subRole, ulong? departmentId, ulong campusId = CampusId) => new()
    {
        UserId = userId,
        FullName = $"User {userId}",
        Email = $"user{userId}@test.local",
        RoleId = roleId,
        SubRole = subRole,
        PrimaryCampusId = campusId,
        DepartmentId = departmentId,
        Status = EntityStatuses.Active,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static VisitRequestCampus CreateVisitInstance(
        ulong visitInstanceId, string status, ulong campusId = CampusId) => new()
    {
        VisitInstanceId = visitInstanceId,
        VisitRequestId = visitInstanceId,
        CampusId = campusId,
        PlannedStartAt = new DateTime(2026, 8, 1, 9, 0, 0),
        PlannedEndAt = new DateTime(2026, 8, 1, 11, 0, 0),
        Status = status,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static VisitParticipant CreateDeptSupportParticipant(
        ulong participantId, ulong visitInstanceId, ulong userId, string status)
        => CreateParticipant(participantId, visitInstanceId, userId, ParticipantRoles.DeptSupport, status);

    public static VisitParticipant CreateParticipant(
        ulong participantId, ulong visitInstanceId, ulong userId, string participantRole, string status) => new()
    {
        ParticipantId = participantId,
        VisitInstanceId = visitInstanceId,
        UserId = userId,
        ParticipantRole = participantRole,
        Status = status,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static UserSession CreateActiveSession(ulong sessionId, ulong userId) => new()
    {
        SessionId = sessionId,
        UserId = userId,
        LoginPortal = "INTERNAL",
        CreatedAt = new DateTime(2026, 7, 1),
        ExpiresAt = new DateTime(2027, 1, 1),
    };

    public static VisitLogisticsItem CreateOpenLogisticsItem(
        ulong logisticsItemId, ulong visitInstanceId, ulong requestedToDepartmentId, string status = "REQUESTED") => new()
    {
        LogisticsItemId = logisticsItemId,
        VisitInstanceId = visitInstanceId,
        ItemType = "ROOM",
        Title = $"Logistics {logisticsItemId}",
        Status = status,
        RequestedToDepartmentId = requestedToDepartmentId,
        CreatedAt = new DateTime(2026, 1, 1),
    };
}
