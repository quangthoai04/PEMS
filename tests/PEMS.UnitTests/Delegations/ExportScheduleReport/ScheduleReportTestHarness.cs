using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
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
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<UploadedFile> Files => Set<UploadedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<UserAuthProvider>();
        modelBuilder.Ignore<UserSession>();
        modelBuilder.Ignore<PartnerContact>();
        modelBuilder.Ignore<PartnerAlias>();
        modelBuilder.Ignore<VisitGuestPartnerLink>();
        modelBuilder.Ignore<VisitInstanceFormDetail>();
        modelBuilder.Ignore<VisitInstanceGuestMember>();
        modelBuilder.Ignore<VisitRequestIdentityChange>();
        modelBuilder.Ignore<VisitRequestIdentityChangeEvent>();
        modelBuilder.Ignore<VisitInstanceAmendment>();
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        modelBuilder.Ignore<VisitLogisticsItem>();
        modelBuilder.Ignore<VisitLogisticsItemHandover>();
        modelBuilder.Ignore<VisitLogisticsAssignmentAttempt>();
        modelBuilder.Ignore<VisitInstanceReminderSetting>();
        modelBuilder.Ignore<VisitPhotoFolder>();
        modelBuilder.Ignore<VisitPhoto>();
        modelBuilder.Ignore<CalendarEvent>();
        modelBuilder.Ignore<EmailTemplate>();
        modelBuilder.Ignore<SentEmail>();
        modelBuilder.Ignore<SentEmailRecipient>();
        modelBuilder.Ignore<EmailActionToken>();
        modelBuilder.Ignore<AuditLog>();
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
        modelBuilder.Ignore<VisitRequestPendingForm>();

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
    DbSet<Document> IApplicationDbContext.Documents => Set<Document>();
    DbSet<VisitInstanceFormDetail> IApplicationDbContext.VisitInstanceFormDetails => Set<VisitInstanceFormDetail>();
    DbSet<VisitInstanceGuestMember> IApplicationDbContext.VisitInstanceGuestMembers => Set<VisitInstanceGuestMember>();
    DbSet<VisitRequestIdentityChange> IApplicationDbContext.VisitRequestIdentityChanges => Set<VisitRequestIdentityChange>();
    DbSet<VisitRequestIdentityChangeEvent> IApplicationDbContext.VisitRequestIdentityChangeEvents => Set<VisitRequestIdentityChangeEvent>();
    DbSet<VisitInstanceAmendment> IApplicationDbContext.VisitInstanceAmendments => Set<VisitInstanceAmendment>();
    DbSet<VisitInstanceAmendmentChange> IApplicationDbContext.VisitInstanceAmendmentChanges => Set<VisitInstanceAmendmentChange>();
    DbSet<VisitInstanceFormRevisionHistory> IApplicationDbContext.VisitInstanceFormRevisionHistories => Set<VisitInstanceFormRevisionHistory>();
    DbSet<VisitRequestRevisionHistory> IApplicationDbContext.VisitRequestRevisionHistories => Set<VisitRequestRevisionHistory>();
    DbSet<VisitRequestPendingForm> IApplicationDbContext.VisitRequestPendingForms => Set<VisitRequestPendingForm>();
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
        DelegationName = "Đoàn khách kiểm thử",
        Purpose = "Tham quan và ký kết hợp tác",
        ContactPersonFullName = "Đầu mối",
        ContactPersonOrganization = "Đối tác",
        ContactPersonPhone = "0900000001",
        ContactPersonEmail = "contact@test.local",
        Status = "APPROVED",
        PartnerId = partnerId,
        VisitorUserId = visitorUserId,
        FormSchemaVersion = 1,
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
