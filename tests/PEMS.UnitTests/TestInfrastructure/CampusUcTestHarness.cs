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
/// EF Core InMemory stand-in for <see cref="IApplicationDbContext"/> used by the UC-86 campus
/// handler/evaluator unit tests. Same pruning approach as <see cref="TestApplicationDbContext"/>
/// (UC-106): only the aggregates this slice touches are PUBLIC DbSet properties; everything else
/// is an explicit interface implementation + explicitly Ignored so it never enters the model.
/// Unlike the UC-106 context, <see cref="VisitRequest"/> IS mapped here (blocker examples join
/// visit_request_campuses → visit_requests for requestCode/delegationName).
/// </summary>
public sealed class CampusTestDbContext : DbContext, IApplicationDbContext
{
    public static CampusTestDbContext Create() =>
        new(new DbContextOptionsBuilder<CampusTestDbContext>()
            .UseInMemoryDatabase($"pems-uc86-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public CampusTestDbContext(DbContextOptions<CampusTestDbContext> options) : base(options) { }

    // ── Mapped aggregates (UC-86 handlers + availability evaluator + blocker calculator) ──
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<VisitRequest> VisitRequests => Set<VisitRequest>();
    public DbSet<VisitRequestCampus> VisitRequestCampuses => Set<VisitRequestCampus>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Prune everything outside the UC-86 slice (see TestApplicationDbContext for rationale).
        modelBuilder.Ignore<UserAuthProvider>();
        modelBuilder.Ignore<VisitAgenda>();
        modelBuilder.Ignore<VisitParticipant>();
        modelBuilder.Ignore<VisitLogisticsItem>();
        modelBuilder.Ignore<VisitLogisticsItemHandover>();
        modelBuilder.Ignore<VisitLogisticsAssignmentAttempt>();
        modelBuilder.Ignore<VisitInstanceReminderSetting>();
        modelBuilder.Ignore<VisitInstanceFormDetail>();
        modelBuilder.Ignore<VisitInstanceGuestMember>();
        modelBuilder.Ignore<VisitRequestIdentityChange>();
        modelBuilder.Ignore<VisitRequestIdentityChangeEvent>();
        modelBuilder.Ignore<VisitInstanceAmendment>();
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        modelBuilder.Ignore<VisitGuestMember>();
        modelBuilder.Ignore<VisitGuestPartnerLink>();
        modelBuilder.Ignore<OtpToken>();
        modelBuilder.Ignore<LoginLog>();
        modelBuilder.Ignore<SecurityEvent>();
        modelBuilder.Ignore<Partner>();
        modelBuilder.Ignore<PartnerContact>();
        modelBuilder.Ignore<PartnerAlias>();
        modelBuilder.Ignore<UploadedFile>();
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
        modelBuilder.Ignore<GalleryItemTtsAudio>();
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
        modelBuilder.Entity<VisitRequest>()
            .HasMany(r => r.CampusInstances).WithOne().HasForeignKey(c => c.VisitRequestId);
        modelBuilder.Entity<UserSession>()
            .HasOne(s => s.User).WithMany(u => u.Sessions).HasForeignKey(s => s.UserId);
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.ActorUser).WithMany().HasForeignKey(a => a.ActorUserId);
    }

    // ── Aggregates the UC-86 slice never touches (NOT discovered by EF) ──────
    DbSet<UserAuthProvider> IApplicationDbContext.UserAuthProviders => Set<UserAuthProvider>();
    DbSet<OtpToken> IApplicationDbContext.OtpTokens => Set<OtpToken>();
    DbSet<LoginLog> IApplicationDbContext.LoginLogs => Set<LoginLog>();
    DbSet<SecurityEvent> IApplicationDbContext.SecurityEvents => Set<SecurityEvent>();
    DbSet<Partner> IApplicationDbContext.Partners => Set<Partner>();
    DbSet<PartnerContact> IApplicationDbContext.PartnerContacts => Set<PartnerContact>();
    DbSet<PartnerAlias> IApplicationDbContext.PartnerAliases => Set<PartnerAlias>();
    DbSet<VisitGuestPartnerLink> IApplicationDbContext.VisitGuestPartnerLinks => Set<VisitGuestPartnerLink>();
    DbSet<UploadedFile> IApplicationDbContext.Files => Set<UploadedFile>();
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
    DbSet<VisitParticipant> IApplicationDbContext.VisitParticipants => Set<VisitParticipant>();
    DbSet<VisitAgenda> IApplicationDbContext.VisitAgendas => Set<VisitAgenda>();
    DbSet<VisitLogisticsItem> IApplicationDbContext.VisitLogisticsItems => Set<VisitLogisticsItem>();
    DbSet<VisitLogisticsItemHandover> IApplicationDbContext.VisitLogisticsItemHandovers => Set<VisitLogisticsItemHandover>();
    DbSet<VisitLogisticsAssignmentAttempt> IApplicationDbContext.VisitLogisticsAssignmentAttempts => Set<VisitLogisticsAssignmentAttempt>();
    DbSet<VisitInstanceReminderSetting> IApplicationDbContext.VisitInstanceReminderSettings => Set<VisitInstanceReminderSetting>();
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
    DbSet<GalleryItemTtsAudio> IApplicationDbContext.GalleryItemTtsAudios => Set<GalleryItemTtsAudio>();
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

/// <summary>
/// Minimal-object builders for the UC-86 fixtures: campuses with complete master data, ACTIVE IC
/// departments, valid Staff Leaders and visit instances per status. Ids are chosen by the caller
/// so assertions stay explicit.
/// </summary>
public static class CampusUcTestData
{
    public const ulong StaffRoleId = 3;
    public const ulong HoRoleId = 2;
    public const ulong DepartmentRoleId = 4;
    public const ulong AdminRoleId = 1;
    public const ulong StudentRoleId = 5;
    public const ulong VisitorRoleId = 6;

    /// <summary>Campus with COMPLETE master data (enable-eligible) — trim fields to test AF cases.</summary>
    public static Campus CreateCampus(ulong campusId, string status = EntityStatuses.Active) => new()
    {
        CampusId = campusId,
        CampusCode = $"C{campusId}",
        Name = $"Campus {campusId}",
        City = "Hà Nội",
        Address = $"Số {campusId} Đường Test",
        Phone = "024 7300 5588",
        Email = $"campus{campusId}@fpt.edu.vn",
        IcHeadUserId = null,
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

    public static Department CreateIcDepartment(
        ulong departmentId, ulong campusId, string status = EntityStatuses.Active) => new()
    {
        DepartmentId = departmentId,
        CampusId = campusId,
        Name = "Phòng Hợp tác Quốc tế",
        DepartmentType = "IC",
        Status = status,
        HeadUserId = null,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static Department CreateGeneralDepartment(
        ulong departmentId, ulong campusId, string status = EntityStatuses.Active) => new()
    {
        DepartmentId = departmentId,
        CampusId = campusId,
        Name = $"Phòng ban {departmentId}",
        DepartmentType = "GENERAL",
        Status = status,
        CreatedAt = new DateTime(2026, 1, 1),
    };

    /// <summary>STAFF + LEADER user of the given campus/department (the §8.3 valid-leader shape).</summary>
    public static User CreateStaffLeader(
        ulong userId, ulong campusId, ulong departmentId, string status = UserStatuses.Active)
        => CreateUser(userId, StaffRoleId, UserSubRoles.Leader, campusId, departmentId, status);

    public static User CreateUser(
        ulong userId, ulong roleId, string? subRole, ulong? campusId, ulong? departmentId,
        string status = UserStatuses.Active) => new()
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

    public static VisitRequest CreateVisitRequest(ulong visitRequestId, string delegationName = "Đoàn Test") => new()
    {
        VisitRequestId = visitRequestId,
        RequestCode = $"VR2026{visitRequestId:D7}",
        CreatedSource = "VISITOR_SUBMITTED",
        RegistrantFullName = "Người đăng ký",
        RegistrantNationality = "VN",
        RegistrantOrganization = "Tổ chức Test",
        RegistrantJobTitle = "Giám đốc",
        RegistrantPhone = "0912345678",
        RegistrantEmail = "registrant@test.local",
        DelegationName = delegationName,
        Purpose = "Tham quan",
        ContactPersonFullName = "Đầu mối",
        ContactPersonOrganization = "Tổ chức Test",
        ContactPersonPhone = "0912345678",
        ContactPersonEmail = "contact@test.local",
        Status = VisitRequestStatuses.PendingApproval,
        SubmittedAt = new DateTime(2026, 6, 1),
        CreatedAt = new DateTime(2026, 6, 1),
    };

    public static VisitRequestCampus CreateVisitInstance(
        ulong visitInstanceId, ulong visitRequestId, ulong campusId, string status) => new()
    {
        VisitInstanceId = visitInstanceId,
        VisitRequestId = visitRequestId,
        CampusId = campusId,
        PlannedStartAt = new DateTime(2026, 8, 1, 9, 0, 0),
        PlannedEndAt = new DateTime(2026, 8, 1, 11, 0, 0),
        Status = status,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    /// <summary>DEPARTMENT + Staff user of a general department in the given campus.</summary>
    public static User CreateDepartmentStaff(ulong userId, ulong campusId, ulong departmentId)
        => CreateUser(userId, DepartmentRoleId, UserSubRoles.Staff, campusId, departmentId);

    /// <summary>STUDENT (sub_role NULL, no department) of the given campus.</summary>
    public static User CreateStudent(ulong userId, ulong campusId)
        => CreateUser(userId, StudentRoleId, null, campusId, null);

    public static UserSession CreateActiveSession(ulong sessionId, ulong userId) => new()
    {
        SessionId = sessionId,
        UserId = userId,
        LoginPortal = "INTERNAL",
        CreatedAt = new DateTime(2026, 7, 1),
        ExpiresAt = new DateTime(2027, 1, 1),
    };
}

/// <summary>
/// Records every <see cref="RevokeAllActiveSessionsAsync"/> call and revokes the matching sessions
/// in the shared campus test context (mirroring the real service, which writes through the same
/// scoped DbContext so its writes join the handler's transaction). All other members throw:
/// UC-86 disable must never call them.
/// </summary>
public sealed class CampusRecordingSessionService : ISessionService
{
    private readonly CampusTestDbContext _db;

    public CampusRecordingSessionService(CampusTestDbContext db) => _db = db;

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
        => throw new NotSupportedException("UC-86 disable must not create sessions.");

    public Task<UserSession?> GetActiveByRefreshTokenAsync(string rawRefreshToken, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-86 disable must not read sessions by refresh token.");

    public Task<bool> IsSessionActiveAsync(ulong sessionId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-86 disable must not probe single sessions.");

    public Task<SessionTokens> RotateRefreshTokenAsync(UserSession session, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-86 disable must not rotate refresh tokens.");

    public Task RevokeSessionAsync(ulong sessionId, string reason, ulong? revokedBy = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-86 disable revokes per-user, not per-session.");
}

/// <summary>
/// Records every <see cref="WriteSecurityEventAsync"/> call (doc 08 §17 — one aggregate
/// security policy event with a CAMPUS_DISABLED_SESSIONS_REVOKED detail marker per successful
/// disable, none on failure).
/// Login-log writes throw: the UC-86 disable flow must never write login logs.
/// </summary>
public sealed class RecordingSecurityAuditService : ISecurityAuditService
{
    public List<(ulong? UserId, string EventType, string Result, ulong? SelectedCampusId, string? DetailText)> Events { get; } = new();

    public Task WriteSecurityEventAsync(
        ulong? userId, string? emailSnapshot, string eventType, string result,
        string? failureReasonCode = null, string? ipAddress = null, string? userAgent = null,
        string? loginPortal = null, ulong? selectedCampusId = null, string? providerType = null,
        ulong? sessionId = null, string? detailText = null, CancellationToken cancellationToken = default)
    {
        Events.Add((userId, eventType, result, selectedCampusId, detailText));
        return Task.CompletedTask;
    }

    public Task WriteLoginLogAsync(
        ulong? userId, string email, string loginPortal, ulong? selectedCampusId,
        string? providerType, string status, string? failureReason, string? ipAddress,
        string? userAgent, ulong? sessionId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("UC-86 disable must not write login logs.");
}
