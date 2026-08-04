using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Models;
using PEMS.Application.Common.Security;
using PEMS.Application.Common.Storage;
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
using PEMS.Shared;

namespace PEMS.UnitTests.Delegations.ExportScheduleReport;

/// <summary>
/// EF Core InMemory stand-in for <see cref="IApplicationDbContext"/> covering the "Báo cáo Lịch
/// trình" slice. Same construction rules as <see cref="PEMS.UnitTests.TestInfrastructure.DelegationsTestDbContext"/>
/// (UC-106) — only the aggregates <see cref="PEMS.Application.Delegations.Queries.ExportScheduleReport.ScheduleReportDataBuilder"/>
/// and its handler touch are PUBLIC DbSet properties; this slice additionally needs VisitAgenda,
/// VisitGuestMember and Partner mapped (the shared DelegationsTestDbContext Ignores all three).
/// </summary>
public class ScheduleReportTestDbContext : DbContext, IApplicationDbContext
{
    public static ScheduleReportTestDbContext Create() =>
        new(new DbContextOptionsBuilder<ScheduleReportTestDbContext>()
            .UseInMemoryDatabase($"pems-schedule-report-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public ScheduleReportTestDbContext(DbContextOptions<ScheduleReportTestDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<VisitRequest> VisitRequests => Set<VisitRequest>();
    public DbSet<VisitRequestCampus> VisitRequestCampuses => Set<VisitRequestCampus>();
    public DbSet<VisitParticipant> VisitParticipants => Set<VisitParticipant>();
    public DbSet<VisitAgenda> VisitAgendas => Set<VisitAgenda>();
    public DbSet<VisitGuestMember> VisitGuestMembers => Set<VisitGuestMember>();
    public DbSet<VisitInstanceGuestMember> VisitInstanceGuestMembers => Set<VisitInstanceGuestMember>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<UploadedFile> Files => Set<UploadedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<UserAuthProvider>();
        modelBuilder.Ignore<UserSession>();
        modelBuilder.Ignore<PartnerContact>();
        modelBuilder.Ignore<PartnerAlias>();
        modelBuilder.Ignore<VisitGuestPartnerLink>();
        // Mapped, not ignored: these tests use the REAL VisitFormReadService, which resolves the report's
        // delegation name and purpose from the campus detail. Ignoring it made the service see no row.
        // Mapped, not ignored: under Pure V2 a guest belongs to the request but is attached to a CAMPUS
        // through this link table, and that is what the real read service enumerates for the report.
        modelBuilder.Ignore<VisitRequestIdentityChange>();
        modelBuilder.Ignore<VisitRequestIdentityChangeEvent>();
        modelBuilder.Ignore<VisitInstanceAmendment>();
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        // Mapped, not ignored: the setup-progress email lists preparation items, so the snapshot
        // builder reads this table and the "no internal field leaks" tests need real rows to check.
        modelBuilder.Ignore<VisitLogisticsItemHandover>();
        modelBuilder.Ignore<VisitLogisticsAssignmentAttempt>();
        modelBuilder.Ignore<VisitInstanceReminderSetting>();
        modelBuilder.Ignore<VisitPhotoFolder>();
        modelBuilder.Ignore<VisitPhoto>();
        modelBuilder.Ignore<CalendarEvent>();
        // Mapped, not ignored: the setup-progress handlers write a real draft (template row, recipients,
        // the attachment and the documents row that identifies the mandatory report), and asserting on
        // what they wrote is the whole point of those tests.
        modelBuilder.Ignore<SentEmail>();
        modelBuilder.Ignore<SentEmailRecipient>();
        modelBuilder.Ignore<EmailActionToken>();
        modelBuilder.Ignore<EmailSendIdempotency>();
        modelBuilder.Ignore<AccountEmailConfirmation>();
        modelBuilder.Ignore<AuditLog>();
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
        modelBuilder.Ignore<Notification>();
        modelBuilder.Ignore<ApiConfiguration>();
        modelBuilder.Ignore<ApiUsageQuota>();
        modelBuilder.Ignore<ApiRequestLog>();
        modelBuilder.Ignore<BusinessCardOcrJob>();
        modelBuilder.Ignore<AgendaTemplate>();
        modelBuilder.Ignore<AgendaTemplateItem>();
        modelBuilder.Ignore<AgendaTemplateDefault>();
        modelBuilder.Ignore<VisitRequestPendingForm>();
        modelBuilder.Ignore<VisitRequestFingerprintGuard>();

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
        modelBuilder.Entity<VisitAgenda>()
            .HasOne(a => a.VisitInstance).WithMany(vc => vc.Agendas)
            .HasForeignKey(a => a.VisitInstanceId);
        modelBuilder.Entity<VisitGuestMember>()
            .HasOne(g => g.VisitRequest).WithMany(v => v.GuestMembers)
            .HasForeignKey(g => g.VisitRequestId);
        modelBuilder.Entity<VisitInstanceGuestMember>()
            .HasKey(l => new { l.VisitInstanceId, l.GuestMemberId });
        modelBuilder.Entity<VisitRequest>()
            .HasOne(v => v.Partner).WithMany().HasForeignKey(v => v.PartnerId);
        modelBuilder.Entity<Partner>()
            .HasOne(p => p.OwnerCampus).WithMany().HasForeignKey(p => p.OwnerCampusId);

        base.OnModelCreating(modelBuilder);
    }

    // ── Aggregates this slice never touches (NOT discovered by EF) ────────────
    DbSet<UserAuthProvider> IApplicationDbContext.UserAuthProviders => Set<UserAuthProvider>();
    DbSet<UserSession> IApplicationDbContext.UserSessions => Set<UserSession>();
    DbSet<OtpToken> IApplicationDbContext.OtpTokens => Set<OtpToken>();
    DbSet<LoginLog> IApplicationDbContext.LoginLogs => Set<LoginLog>();
    DbSet<SecurityEvent> IApplicationDbContext.SecurityEvents => Set<SecurityEvent>();
    DbSet<PartnerTranslation> IApplicationDbContext.PartnerTranslations => Set<PartnerTranslation>();
    DbSet<PartnerContact> IApplicationDbContext.PartnerContacts => Set<PartnerContact>();
    DbSet<PartnerAlias> IApplicationDbContext.PartnerAliases => Set<PartnerAlias>();
    DbSet<VisitGuestPartnerLink> IApplicationDbContext.VisitGuestPartnerLinks => Set<VisitGuestPartnerLink>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<VisitInstanceFormDetail> VisitInstanceFormDetails => Set<VisitInstanceFormDetail>();
    DbSet<VisitRequestIdentityChange> IApplicationDbContext.VisitRequestIdentityChanges => Set<VisitRequestIdentityChange>();
    DbSet<VisitRequestIdentityChangeEvent> IApplicationDbContext.VisitRequestIdentityChangeEvents => Set<VisitRequestIdentityChangeEvent>();
    DbSet<VisitInstanceAmendment> IApplicationDbContext.VisitInstanceAmendments => Set<VisitInstanceAmendment>();
    DbSet<VisitInstanceAmendmentChange> IApplicationDbContext.VisitInstanceAmendmentChanges => Set<VisitInstanceAmendmentChange>();
    DbSet<VisitInstanceFormRevisionHistory> IApplicationDbContext.VisitInstanceFormRevisionHistories => Set<VisitInstanceFormRevisionHistory>();
    DbSet<VisitRequestRevisionHistory> IApplicationDbContext.VisitRequestRevisionHistories => Set<VisitRequestRevisionHistory>();
    DbSet<VisitRequestPendingForm> IApplicationDbContext.VisitRequestPendingForms => Set<VisitRequestPendingForm>();
    DbSet<VisitRequestFingerprintGuard> IApplicationDbContext.VisitRequestFingerprintGuards => Set<VisitRequestFingerprintGuard>();
    public DbSet<VisitLogisticsItem> VisitLogisticsItems => Set<VisitLogisticsItem>();
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
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    DbSet<SentEmail> IApplicationDbContext.SentEmails => Set<SentEmail>();
    DbSet<SentEmailRecipient> IApplicationDbContext.SentEmailRecipients => Set<SentEmailRecipient>();
    DbSet<SentEmailAttachment> IApplicationDbContext.SentEmailAttachments => Set<SentEmailAttachment>();
    DbSet<EmailActionToken> IApplicationDbContext.EmailActionTokens => Set<EmailActionToken>();
    DbSet<EmailSendIdempotency> IApplicationDbContext.EmailSendIdempotencies => Set<EmailSendIdempotency>();
    DbSet<EmailContactPolicy> IApplicationDbContext.EmailContactPolicies => Set<EmailContactPolicy>();
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
}

/// <summary>Fixture builders for the schedule-report tests. One campus (id 1), a host (STAFF, id 100)
/// assigned to instance 10 of visit request 10; ids of extra rows are chosen by each test.</summary>
public static class ScheduleReportTestData
{
    public const ulong CampusId = 1;
    public const ulong StaffRoleId = 3;
    public const ulong DepartmentRoleId = 4;
    public const ulong VisitorRoleId = 6;
    public const ulong HoRoleId = 7;

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

    public static Department CreateDepartment(ulong departmentId, ulong campusId = CampusId) => new()
    {
        DepartmentId = departmentId,
        CampusId = campusId,
        Name = $"Phòng ban {departmentId}",
        DepartmentType = "IC",
        Status = EntityStatuses.Active,
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

    public static VisitRequest CreateVisitRequest(
        ulong visitRequestId = VisitRequestId, ulong? partnerId = null, ulong? visitorUserId = null) => new()
    {
        VisitRequestId = visitRequestId,
        RequestCode = $"VR-{visitRequestId}",
        RegistrantFullName = "Nguyễn Văn Khách",
        RegistrantNationality = "VN",
        RegistrantOrganization = "Đối tác",
        RegistrantJobTitle = "Trưởng đoàn",
        RegistrantPhone = "0900000000",
        RegistrantEmail = "guest@test.local",
        // Pure V2: delegation name and purpose are per campus and live on VisitInstanceFormDetail (see
        // CreateVisitInstance). The request row keeps only the PRIMARY contact — a request-level relation,
        // distinct from each campus's operational contact.
        HasMixedCampusDetails = false,
        ContactPersonFullName = "Đầu mối",
        ContactPersonOrganization = "Đối tác",
        ContactPersonPhone = "0900000001",
        ContactPersonEmail = "contact@test.local",
        Status = "APPROVED",
        PartnerId = partnerId,
        VisitorUserId = visitorUserId,
        SubmittedAt = new DateTime(2026, 6, 1),
        CreatedAt = new DateTime(2026, 6, 1),
    };

    /// <summary>
    /// Every instance owns exactly one form detail — that is where the schedule report reads its
    /// delegation name and purpose from. <paramref name="delegationName"/> and <paramref name="purpose"/>
    /// are per campus so a multi-campus test can prove the report never borrows a sibling's content.
    /// </summary>
    public static VisitRequestCampus CreateVisitInstance(
        ulong visitInstanceId = VisitInstanceId,
        string status = VisitInstanceStatus.BeforeVisit,
        ulong campusId = CampusId,
        ulong? currentHostUserId = HostUserId,
        ulong visitRequestId = VisitRequestId,
        string delegationName = "Đoàn khách kiểm thử",
        string purpose = "Tham quan và ký kết hợp tác") => new()
    {
        VisitInstanceId = visitInstanceId,
        VisitRequestId = visitRequestId,
        CampusId = campusId,
        PlannedStartAt = new DateTime(2026, 8, 1, 9, 0, 0),
        PlannedEndAt = new DateTime(2026, 8, 1, 11, 0, 0),
        Status = status,
        CurrentHostUserId = currentHostUserId,
        CreatedAt = new DateTime(2026, 6, 1),
        FormDetail = new VisitInstanceFormDetail
        {
            VisitInstanceId = visitInstanceId,
            DelegationName = delegationName,
            VisitType = "MEETING",
            Purpose = purpose,
            OperationalContactFullName = "Đầu mối cơ sở",
            OperationalContactPhone = "0900000002",
            WorkingLanguage = "VI",
            MediaConsentStatus = "AGREED",
            CreatedAt = new DateTime(2026, 6, 1),
        },
    };

    public static VisitParticipant CreateParticipant(
        ulong participantId, ulong userId, string participantRole, string status,
        bool isHost = false, ulong visitInstanceId = VisitInstanceId) => new()
    {
        ParticipantId = participantId,
        VisitInstanceId = visitInstanceId,
        UserId = userId,
        ParticipantRole = participantRole,
        IsHost = isHost,
        Status = status,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    public static VisitAgenda CreateAgenda(
        ulong agendaId, string title, DateTime start, DateTime? end = null,
        string? location = null, int sequenceOrder = 0, ulong visitInstanceId = VisitInstanceId) => new()
    {
        AgendaId = agendaId,
        VisitInstanceId = visitInstanceId,
        Title = title,
        StartTime = start,
        EndTime = end,
        Location = location,
        SequenceOrder = sequenceOrder,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    public static VisitGuestMember CreateGuestMember(
        ulong guestMemberId, string fullName, string memberType = "GUEST",
        uint displayOrder = 0, ulong visitRequestId = VisitRequestId) => new()
    {
        GuestMemberId = guestMemberId,
        VisitRequestId = visitRequestId,
        MemberType = memberType,
        DisplayOrder = displayOrder,
        FullName = fullName,
        Organization = "Guest Org",
        JobTitle = "Member",
        Nationality = "VN",
        CreatedAt = new DateTime(2026, 6, 1),
    };

    /// <summary>
    /// Attaches a guest to a campus instance. Pure V2 reads the roster through this link, so a guest with
    /// no link belongs to no campus and correctly appears on no report.
    /// </summary>
    public static VisitInstanceGuestMember CreateInstanceGuestLink(
        ulong guestMemberId, uint displayOrder = 0,
        ulong visitInstanceId = VisitInstanceId, ulong visitRequestId = VisitRequestId) => new()
    {
        VisitRequestId = visitRequestId,
        VisitInstanceId = visitInstanceId,
        GuestMemberId = guestMemberId,
        DisplayOrder = displayOrder,
        CreatedAt = new DateTime(2026, 6, 1),
    };

    public static Partner CreatePartner(ulong partnerId, string name, ulong? logoFileId, ulong campusId = CampusId) => new()
    {
        PartnerId = partnerId,
        OwnerCampusId = campusId,
        Name = name,
        PartnerType = "COMPANY",
        LogoFileId = logoFileId,
        ProfileStatus = "APPROVED",
        Visibility = "PUBLIC",
        CooperationStatus = "ACTIVE",
        CreatedAt = new DateTime(2026, 1, 1),
    };

    public static UploadedFile CreateFile(ulong fileId, string storageProvider = "LOCAL") => new()
    {
        FileId = fileId,
        StorageProvider = storageProvider,
        ObjectKey = $"logo-{fileId}.png",
        OriginalFilename = $"logo-{fileId}.png",
        MimeType = "image/png",
        UploadedAt = new DateTime(2026, 1, 1),
    };

    /// <summary>Seeds the shared skeleton: campus, roles, the host (STAFF of an IC department on
    /// campus 1) and the visit request + instance hosted by them.</summary>
    public static (VisitRequestCampus Instance, User Host) SeedBase(
        ScheduleReportTestDbContext db, string instanceStatus = VisitInstanceStatus.BeforeVisit,
        ulong? partnerId = null, ulong? visitorUserId = null)
    {
        db.Campuses.Add(CreateCampus());
        db.Roles.AddRange(
            CreateRole(StaffRoleId, RoleCodes.Staff),
            CreateRole(DepartmentRoleId, RoleCodes.Department),
            CreateRole(VisitorRoleId, RoleCodes.Visitor),
            CreateRole(HoRoleId, RoleCodes.Ho));

        var icDept = CreateDepartment(900);
        db.Departments.Add(icDept);

        var host = CreateUser(HostUserId, StaffRoleId, UserSubRoles.Staff, icDept.DepartmentId);
        db.Users.Add(host);

        db.VisitRequests.Add(CreateVisitRequest(partnerId: partnerId, visitorUserId: visitorUserId));
        var instance = CreateVisitInstance(status: instanceStatus);
        db.VisitRequestCampuses.Add(instance);
        db.SaveChanges();
        return (instance, host);
    }

    /// <summary>
    /// One MIXED request spanning three campuses, each with its own delegation name, purpose,
    /// operational contact and guest. Nothing is shared between them, so a report that borrows from a
    /// sibling — or from the request row — produces a value that provably belongs to another campus.
    ///
    /// Returns the three instances ordered A, B, C.
    /// </summary>
    public static IReadOnlyList<VisitRequestCampus> SeedMixedThreeCampuses(ScheduleReportTestDbContext db)
    {
        db.Campuses.AddRange(CreateCampus(1), CreateCampus(2), CreateCampus(3));
        db.Roles.AddRange(
            CreateRole(StaffRoleId, RoleCodes.Staff),
            CreateRole(DepartmentRoleId, RoleCodes.Department),
            CreateRole(VisitorRoleId, RoleCodes.Visitor),
            CreateRole(HoRoleId, RoleCodes.Ho));

        var icDept = CreateDepartment(900);
        db.Departments.Add(icDept);
        db.Users.Add(CreateUser(HostUserId, StaffRoleId, UserSubRoles.Staff, icDept.DepartmentId));

        var request = CreateVisitRequest();
        request.HasMixedCampusDetails = true;
        db.VisitRequests.Add(request);

        var tags = new[] { "A", "B", "C" };
        var instances = new List<VisitRequestCampus>();
        for (var i = 0; i < tags.Length; i++)
        {
            var tag = tags[i];
            var instance = CreateVisitInstance(
                visitInstanceId: (ulong)(10 + i),
                campusId: (ulong)(i + 1),
                delegationName: $"Đoàn {tag}",
                purpose: $"Mục đích {tag}");
            instance.FormDetail!.OperationalContactFullName = $"Đầu mối {tag}";
            instances.Add(instance);
            db.VisitRequestCampuses.Add(instance);

            // One guest per campus, linked ONLY to that campus.
            var guestId = (ulong)(i + 1);
            db.VisitGuestMembers.Add(CreateGuestMember(guestId, $"Khách {tag}"));
            db.VisitInstanceGuestMembers.Add(
                CreateInstanceGuestLink(guestId, visitInstanceId: instance.VisitInstanceId));
        }

        db.SaveChanges();
        return instances;
    }
}

/// <summary>
/// Disk storage that always succeeds, unless a file id is listed in <see cref="Unreadable"/> — the
/// shape of "the row is there, the bytes are not".
/// </summary>
public sealed class StubFileStorage : IFileStorageService
{
    /// <summary>File ids whose bytes cannot be read, as a purged or unreachable file would behave.</summary>
    public HashSet<ulong> Unreadable { get; } = new();

    public byte[] Payload { get; set; } = { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"

    public Task<StoredFileInfo> SaveAsync(
        Stream content, string originalFilename, string? contentType, string? purpose,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("These tests never write through IFileStorageService.");

    public Task<Stream?> OpenReadAsync(UploadedFile file, CancellationToken cancellationToken = default)
        => Task.FromResult<Stream?>(
            Unreadable.Contains(file.FileId) ? null : new MemoryStream(Payload, writable: false));
}

/// <summary>
/// Google Drive for the unit suite: serves bytes for anything, or fails every read the way the real
/// client does — with a <see cref="BusinessRuleException"/> carrying a <see cref="StorageErrorCodes"/>
/// value. Failing with the production exception (rather than a bare throw) is what lets a test assert
/// that a deleted file and a refused file are told apart downstream.
/// </summary>
public sealed class StubGoogleDriveStorage : IGoogleDriveStorageService
{
    /// <summary>When set, every download fails this way.</summary>
    public BusinessRuleException? DownloadFailure { get; set; }

    /// <summary>
    /// A real 1×1 PNG, not just the magic bytes. The schedule report hands whatever comes back to
    /// QuestPDF, which genuinely decodes it — a placeholder header throws
    /// <c>DocumentComposeException</c> and would make the happy path look like a storage failure.
    /// </summary>
    public byte[] Payload { get; set; } = System.Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>Proof of whether Drive was consulted at all — a broken row must not reach the network.</summary>
    public int DownloadCalls { get; private set; }

    /// <summary>Drive answered 404: deleted, trashed, or invisible to the credential.</summary>
    public static BusinessRuleException Deleted() => new(
        "Không đọc được tệp trên Google Drive: tệp không tồn tại, đã bị xoá, hoặc tài khoản dịch vụ "
        + "không được chia sẻ tệp này.", StorageErrorCodes.FileNotFound);

    /// <summary>Drive answered 403: the file is there and the read was refused.</summary>
    public static BusinessRuleException PermissionDenied() => new(
        "Google Drive từ chối quyền đọc tệp này.", StorageErrorCodes.FileForbidden);

    public Task<Stream> DownloadAsync(string externalFileId, CancellationToken cancellationToken = default)
    {
        DownloadCalls++;
        if (DownloadFailure is not null) throw DownloadFailure;
        return Task.FromResult<Stream>(new MemoryStream(Payload, writable: false));
    }

    // ── Not reachable from the flows these tests exercise ───────────────────
    public Task<GoogleDriveUploadResult> UploadAvatarAsync(
        byte[] content, string driveFileName, string contentType, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Avatar upload is not part of any flow using this double.");

    public Task<GoogleDriveUploadResult> UploadFileAsync(
        byte[] content, string driveFileName, string contentType, string? folderId = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Upload is not part of any flow using this double.");

    public Task<GoogleDriveDownloadResult> DownloadRangeAsync(
        string externalFileId, long? from, long? to, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Ranged download is not part of any flow using this double.");

    public Task DeleteAsync(string externalFileId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<GoogleDriveFolderResult> EnsureChildFolderAsync(
        string folderName, string parentFolderId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Folder creation is not part of any flow using this double.");
}

/// <summary>Mutable current-user stub defaulting to the instance host of the fixtures.</summary>
public sealed class FakeScheduleReportCurrentUser : ICurrentUserService
{
    public bool IsAuthenticated { get; set; } = true;
    public ulong? UserId { get; set; } = ScheduleReportTestData.HostUserId;
    public string? Email { get; set; } = "host@test.local";
    public ulong? RoleId { get; set; } = ScheduleReportTestData.StaffRoleId;
    public string? RoleCode { get; set; } = RoleCodes.Staff;
    public string? SubRole { get; set; } = UserSubRoles.Staff;
    public ulong? PrimaryCampusId { get; set; } = ScheduleReportTestData.CampusId;
    public ulong? DepartmentId { get; set; }
    public ulong? SessionId { get; set; } = 1;
    public string? LoginPortal { get; set; } = "INTERNAL";
}
