using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Application.Emails.Common;
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
    public DbSet<VisitInstanceReminderSetting> ReminderSettings => Set<VisitInstanceReminderSetting>();
    public DbSet<VisitLogisticsItemHandover> LogisticsHandovers => Set<VisitLogisticsItemHandover>();
    public DbSet<VisitLogisticsAssignmentAttempt> LogisticsAssignmentAttempts => Set<VisitLogisticsAssignmentAttempt>();
    public DbSet<VisitExpenseReport> ExpenseReports => Set<VisitExpenseReport>();
    // Student visit photo storage slice (upload/list/remove handlers + folder service).
    public DbSet<VisitPhotoFolder> VisitPhotoFolders => Set<VisitPhotoFolder>();
    public DbSet<VisitPhoto> VisitPhotos => Set<VisitPhoto>();
    // The folder search matches on tagged people and on the delegation's own members.
    public DbSet<PhotoFaceTag> FaceTags => Set<PhotoFaceTag>();
    public DbSet<VisitGuestMember> GuestMembers => Set<VisitGuestMember>();
    public DbSet<VisitInstanceGuestMember> InstanceGuestMembers => Set<VisitInstanceGuestMember>();
    public DbSet<UploadedFile> Files => Set<UploadedFile>();
    // Visit-news slice (GetVisitInstanceNewsQueryHandler, ViewNewsListQueryHandler): both .Include()
    // Translations.Sections, so both entity types must be part of the model, not just News itself.
    public DbSet<News> News => Set<News>();
    public DbSet<NewsTranslation> NewsTranslations => Set<NewsTranslation>();
    public DbSet<NewsContentSection> NewsContentSections => Set<NewsContentSection>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<SentEmail> SentEmails => Set<SentEmail>();
    public DbSet<SentEmailRecipient> SentEmailRecipients => Set<SentEmailRecipient>();
    public DbSet<SentEmailAttachment> Attachments => Set<SentEmailAttachment>();
    public DbSet<EmailActionToken> EmailActionTokens => Set<EmailActionToken>();
    public DbSet<EmailSendIdempotency> EmailSendIdempotencies => Set<EmailSendIdempotency>();
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
        // Pure V2: form content lives here, so the slice MUST map it — pruning it would make every
        // handler read a null detail and silently lose the delegation name / purpose.
        // Guest members and their per-campus links are NOT pruned either: the photo-folder search
        // matches a delegation by the name or organization of the people in it, so a slice that
        // cannot see them cannot tell "no match" from "the table is not in the model".
        modelBuilder.Ignore<VisitRequestIdentityChange>();
        modelBuilder.Ignore<VisitRequestIdentityChangeEvent>();
        // Amendments are NOT pruned either: the management list counts the ones awaiting a decision on
        // the campuses the caller can see, which is what drives both the "cần duyệt thay đổi" badge and
        // the REVIEW_AMENDMENT next task.
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        // Agendas are NOT pruned either: VisitNextTaskBuilder reads them (LoadPreparationBlockedAsync)
        // for every row ViewGuestDelegationListQueryHandler returns, same rule as the amendments above.
        // Handovers + assignment attempts are NOT pruned: the assignee-assignment and expense-reminder
        // handlers read both to decide whether a task may still be reassigned and whether its expense
        // entry is open, so by this slice's own rule ("map what these handlers use") they belong here.
        // Reminder settings are NOT pruned: the reminder dispatch service reads and claims them.
        modelBuilder.Ignore<CalendarEventAttendee>();
        modelBuilder.Ignore<CalendarEventReminder>();
        // Mapped, not ignored: the report senders (Batch 9) write a files row and link it here, and a
        // test that cannot see that row cannot tell "attached" from "said it attached".
        modelBuilder.Ignore<AuditLogChange>();
        modelBuilder.Ignore<OtpToken>();
        modelBuilder.Ignore<LoginLog>();
        modelBuilder.Ignore<SecurityEvent>();
        modelBuilder.Ignore<Document>();
        // Minutes are NOT pruned: MinuteAutoFill builds the biên bản attendance list out of this
        // slice's own participants and guest members, so by this slice's rule ("map what these
        // handlers use") it belongs here — an unmapped DbSet would turn "no duplicate" into "the
        // table is not in the model".
        modelBuilder.Ignore<Feedback>();
        // News/NewsTranslation/NewsContentSection ARE mapped (below, as public DbSets) — the visit-news
        // handlers (VisitNewsAccess-gated: GetVisitInstanceNewsQueryHandler, ViewNewsListQueryHandler)
        // belong to this slice and .Include() their Translations/Sections. NewsSectionFile stays
        // ignored: nothing in this slice reads it.
        modelBuilder.Ignore<NewsSectionFile>();
        modelBuilder.Ignore<Faq>();
        modelBuilder.Ignore<GalleryArea>();
        modelBuilder.Ignore<GalleryLocation>();
        modelBuilder.Ignore<GalleryItem>();
        modelBuilder.Ignore<GalleryItemMedia>();
        modelBuilder.Ignore<GalleryItemContent>();
        // Expense reports are read by the reminder handler to work out who has not filed one yet.
        modelBuilder.Ignore<VisitExpenseItem>();
        modelBuilder.Ignore<VisitExpenseReportEvent>();
        // Face tags are NOT pruned: the photo-folder search matches a delegation by the name of a
        // person tagged in one of its photos.
        // Notifications are NOT pruned: the management-list query reads them to work out what has
        // changed on a row since the caller last looked, so by this slice's own rule ("map what these
        // handlers use") they belong in the model.
        modelBuilder.Ignore<AccountEmailConfirmation>();
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

        // ── Per-campus form v2 guest members + face tags ──────────────────────
        // Same shape as the production ApplicationDbContext: the composite FKs of the link table bind
        // member and instance to the SAME request, so they need the two alternate keys. Modelling
        // this here (rather than pruning it) is what lets the folder-search tests exercise the guest
        // and face-tag branches of the query instead of blowing up on an unmapped DbSet.
        modelBuilder.Entity<VisitRequestCampus>()
            .HasAlternateKey(vc => new { vc.VisitRequestId, vc.VisitInstanceId });
        modelBuilder.Entity<VisitGuestMember>()
            .HasAlternateKey(g => new { g.VisitRequestId, g.GuestMemberId });
        modelBuilder.Entity<VisitGuestMember>()
            .HasOne(g => g.VisitRequest).WithMany(v => v.GuestMembers)
            .HasForeignKey(g => g.VisitRequestId);
        modelBuilder.Entity<VisitInstanceGuestMember>(b =>
        {
            b.HasKey(l => new { l.VisitInstanceId, l.GuestMemberId });
            b.HasOne(l => l.VisitInstance).WithMany(vc => vc.GuestMemberLinks)
                .HasForeignKey(l => new { l.VisitRequestId, l.VisitInstanceId })
                .HasPrincipalKey(vc => new { vc.VisitRequestId, vc.VisitInstanceId });
            b.HasOne(l => l.GuestMember).WithMany(g => g.InstanceLinks)
                .HasForeignKey(l => new { l.VisitRequestId, l.GuestMemberId })
                .HasPrincipalKey(g => new { g.VisitRequestId, g.GuestMemberId });
        });
        // ── Minutes (same relationship shape as the production ApplicationDbContext) ──
        modelBuilder.Entity<Minute>()
            .HasOne<VisitRequestCampus>().WithMany().HasForeignKey(m => m.VisitInstanceId);
        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(p => p.Minute).WithMany(m => m.Participants).HasForeignKey(p => p.MinutesId);
        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId);
        // The second User navigation is the attendance checker; without this EF would invent a shadow
        // FK column rather than using checked_by.
        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(p => p.CheckedByUser).WithMany().HasForeignKey(p => p.CheckedBy);
        modelBuilder.Entity<MinuteParticipant>()
            .HasOne(p => p.GuestMember).WithMany().HasForeignKey(p => p.GuestMemberId);
        modelBuilder.Entity<MinuteActionItem>()
            .HasOne(a => a.Minute).WithMany(m => m.ActionItems).HasForeignKey(a => a.MinutesId);

        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.File).WithMany().HasForeignKey(ft => ft.FileId);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.TaggedUser).WithMany().HasForeignKey(ft => ft.TaggedUserId);
        modelBuilder.Entity<PhotoFaceTag>()
            .HasOne(ft => ft.VisitRequest).WithMany().HasForeignKey(ft => ft.VisitRequestId);

        // NOTE: the DB unique keys of the visit-photo slice (one folder per request, one photo per
        // files row) are NOT modeled here — the InMemory provider only enforces primary/alternate
        // KEYS, not unique indexes. Tests that need the duplicate-key failure path simulate the
        // DbUpdateException deterministically via a SaveChanges-failing subclass.
    }

    // ── Aggregates this slice never touches (NOT discovered by EF) ────────────
    DbSet<UserAuthProvider> IApplicationDbContext.UserAuthProviders => Set<UserAuthProvider>();
    DbSet<AccountEmailConfirmation> IApplicationDbContext.AccountEmailConfirmations => Set<AccountEmailConfirmation>();
    DbSet<UserSession> IApplicationDbContext.UserSessions => Set<UserSession>();
    DbSet<OtpToken> IApplicationDbContext.OtpTokens => Set<OtpToken>();
    DbSet<LoginLog> IApplicationDbContext.LoginLogs => Set<LoginLog>();
    DbSet<SecurityEvent> IApplicationDbContext.SecurityEvents => Set<SecurityEvent>();
    DbSet<Partner> IApplicationDbContext.Partners => Set<Partner>();
    DbSet<PartnerTranslation> IApplicationDbContext.PartnerTranslations => Set<PartnerTranslation>();
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
    // News/NewsTranslation/NewsContentSection are public DbSets above; only NewsSectionFile (unused
    // by this slice) stays an undiscovered explicit-interface member.
    DbSet<NewsSectionFile> IApplicationDbContext.NewsSectionFiles => Set<NewsSectionFile>();
    DbSet<Faq> IApplicationDbContext.Faqs => Set<Faq>();
    DbSet<FaqTranslation> IApplicationDbContext.FaqTranslations => Set<FaqTranslation>();
    DbSet<GalleryArea> IApplicationDbContext.GalleryAreas => Set<GalleryArea>();
    DbSet<GalleryLocation> IApplicationDbContext.GalleryLocations => Set<GalleryLocation>();
    DbSet<GalleryItem> IApplicationDbContext.GalleryItems => Set<GalleryItem>();
    DbSet<GalleryItemMedia> IApplicationDbContext.GalleryItemMedia => Set<GalleryItemMedia>();
    DbSet<GalleryItemContent> IApplicationDbContext.GalleryItemContents => Set<GalleryItemContent>();
    DbSet<PhotoFaceTag> IApplicationDbContext.PhotoFaceTags => Set<PhotoFaceTag>();
    DbSet<SentEmailAttachment> IApplicationDbContext.SentEmailAttachments => Set<SentEmailAttachment>();
    DbSet<Notification> IApplicationDbContext.Notifications => Set<Notification>();
    DbSet<CalendarEvent> IApplicationDbContext.CalendarEvents => CalendarEvents;
    DbSet<ApiConfiguration> IApplicationDbContext.ApiConfigurations => Set<ApiConfiguration>();
    DbSet<ApiUsageQuota> IApplicationDbContext.ApiUsageQuotas => Set<ApiUsageQuota>();
    DbSet<ApiRequestLog> IApplicationDbContext.ApiRequestLogs => Set<ApiRequestLog>();
    DbSet<BusinessCardOcrJob> IApplicationDbContext.BusinessCardOcrJobs => Set<BusinessCardOcrJob>();
    DbSet<VisitPhotoFaceScan> IApplicationDbContext.VisitPhotoFaceScans => Set<VisitPhotoFaceScan>();
    DbSet<VisitPhotoFaceDetection> IApplicationDbContext.VisitPhotoFaceDetections => Set<VisitPhotoFaceDetection>();
    DbSet<AgendaTemplate> IApplicationDbContext.AgendaTemplates => Set<AgendaTemplate>();
    DbSet<AgendaTemplateItem> IApplicationDbContext.AgendaTemplateItems => Set<AgendaTemplateItem>();
    DbSet<AgendaTemplateDefault> IApplicationDbContext.AgendaTemplateDefaults => Set<AgendaTemplateDefault>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
        => Database.BeginTransactionAsync(cancellationToken);

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginSerializedTransactionAsync(
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
        // Pure V2: delegation name, purpose and the operational contact live in the per-campus
        // detail, not on the request row.
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
        // Pure V2: every campus instance owns exactly one form detail. Seeding it here keeps the
        // invariant true for every handler under test, and gives each instance its OWN content.
        FormDetail = new VisitInstanceFormDetail
        {
            VisitInstanceId = visitInstanceId,
            DelegationName = "Đoàn khách kiểm thử",
            VisitType = "MEETING",
            Purpose = "Tham quan",
            WorkingContent = "Nội dung kiểm thử",
            OperationalContactFullName = "Đầu mối cơ sở",
            OperationalContactOrganization = "Đối tác",
            OperationalContactJobTitle = "Trưởng phòng Hợp tác",
            OperationalContactPhone = "0900000002",
            OperationalContactEmail = "op@test.local",
            WorkingLanguage = "EN",
            MediaConsentStatus = "AGREED",
            FormRevision = 1,
            ApprovalRevision = 1,
            CreatedAt = new DateTime(2026, 6, 1),
        },
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
    public RecordingUserMutationLockService Locks { get; } = new();
    public FakeDateTimeService Clock { get; } = new();

    public List<OutboundEmail> SentEmails { get; } = new();
    public bool FailEmail { get; set; }

    public DelegationsHandlerMocks()
    {
        var seq = 0;
        Tokens.Setup(t => t.GenerateRawToken()).Returns(() => $"raw-token-{++seq}");
        Tokens.Setup(t => t.Hash(It.IsAny<string>())).Returns<string>(raw => $"hash({raw})");
        Tokens.Setup(t => t.BuildPublicActionUrl(It.IsAny<string>())).Returns<string>(raw => $"https://pems.test/email-actions/{raw}");
        Tokens.Setup(t => t.BuildVisitParticipantAssignmentUrl(It.IsAny<ulong>()))
            .Returns((ulong p) => $"https://pems.test/departments/assign/{p}");
        Tokens.Setup(t => t.BuildDepartmentStaffLogisticsTaskUrl(It.IsAny<ulong>()))
            .Returns((ulong id) => $"https://pems.test/dashboard?taskId={id}&itemType=REQUEST");
        Tokens.Setup(t => t.BuildDepartmentLeaderLogisticsTaskUrl(It.IsAny<ulong>()))
            .Returns((ulong id) => $"https://pems.test/dashboard/visit?taskId={id}&itemType=REQUEST");
        Tokens.Setup(t => t.BuildHostVisitProcessUrl(It.IsAny<ulong>()))
            .Returns((ulong id) => $"https://pems.test/visit/process/{id}");
        Tokens.Setup(t => t.BuildVisitContributionUrl(It.IsAny<ulong>()))
            .Returns((ulong id) => $"https://pems.test/visit/contribution/{id}");

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

    /// <summary>
    /// A dispatcher bound to this test database. It writes the same <c>sent_emails</c> /
    /// <c>sent_email_recipients</c> rows the real one writes, so a handler's in-transaction linkage —
    /// <c>email_action_tokens.sent_email_id</c> — is exercised for real. What it does NOT do is read
    /// <c>email_templates</c>: template content, the variable contract and the subject guard are proven
    /// against a real database by the integration suite, not re-implemented here.
    /// </summary>
    public FakeDelegationsEmailDispatcher DispatcherFor(DelegationsTestDbContext db) => new(db, this);
}

/// <summary>
/// See <see cref="DelegationsHandlerMocks.DispatcherFor"/>. Records every request so a test can assert
/// WHICH TEMPLATE a handler asked for — a handler that quietly went back to composing its own content
/// would name none.
/// </summary>
public sealed class FakeDelegationsEmailDispatcher : ISystemEmailDispatcher
{
    private readonly DelegationsTestDbContext _db;
    private readonly DelegationsHandlerMocks _mocks;

    public FakeDelegationsEmailDispatcher(DelegationsTestDbContext db, DelegationsHandlerMocks mocks)
    {
        _db = db;
        _mocks = mocks;
    }

    /// <summary>Every request, in order, including ones whose delivery later failed.</summary>
    public List<SystemEmailRequest> Requests { get; } = new();

    public SystemEmailRequest Single(string templateCode)
        => Requests.Single(r => r.TemplateCode == templateCode);

    /// <summary>Every request made with this template code, in order.</summary>
    public IReadOnlyList<SystemEmailRequest> All(string templateCode)
        => Requests.Where(r => r.TemplateCode == templateCode).ToList();

    public async Task<PreparedSystemEmail> PrepareAsync(
        SystemEmailRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);

        var now = _mocks.Clock.VietnamNow;
        var sentEmail = new SentEmail
        {
            RelatedType = request.RelatedType,
            RelatedId = request.RelatedId,
            Subject = $"[{request.TemplateCode}]",
            BodySnapshot = null,
            BodyFormat = PEMS.Domain.Enums.EmailBodyFormat.HTML,
            Status = "QUEUED",
            SentBy = request.SentBy,
            CreatedAt = now,
            LastAttemptAt = now,
        };
        sentEmail.Recipients.Add(new SentEmailRecipient
        {
            RecipientEmail = request.To.Email,
            RecipientName = request.To.DisplayName,
            RecipientType = EmailRecipientTypes.To,
            DeliveryStatus = "QUEUED",
            CreatedAt = now,
        });

        _db.SentEmails.Add(sentEmail);
        await _db.SaveChangesAsync(cancellationToken);

        return new PreparedSystemEmail(
            sentEmail.SentEmailId,
            sentEmail.Recipients.First().SentEmailRecipientId,
            EmailTemplateId: 1,
            request.TemplateCode,
            request.To,
            sentEmail.Subject,
            Body: string.Empty,
            IsHtml: true)
        {
            Attachments = request.Attachments ?? Array.Empty<OutboundAttachment>(),
        };
    }

    public async Task<EmailDeliveryResult> DeliverAsync(
        PreparedSystemEmail prepared, CancellationToken cancellationToken = default)
    {
        var sentEmail = await _db.SentEmails
            .FirstOrDefaultAsync(e => e.SentEmailId == prepared.SentEmailId, cancellationToken);
        var recipient = await _db.SentEmailRecipients
            .FirstOrDefaultAsync(r => r.SentEmailRecipientId == prepared.SentEmailRecipientId, cancellationToken);

        var now = _mocks.Clock.VietnamNow;
        if (sentEmail is not null) sentEmail.LastAttemptAt = now;

        if (_mocks.FailEmail)
        {
            if (sentEmail is not null)
            {
                sentEmail.Status = "FAILED";
                sentEmail.ErrorMessage = "SMTP down (test)";
            }
            if (recipient is not null) recipient.DeliveryStatus = "FAILED";
            await _db.SaveChangesAsync(cancellationToken);
            return EmailDeliveryResult.Failed("SMTP_DOWN", "SMTP down (test)");
        }

        _mocks.SentEmails.Add(new OutboundEmail
        {
            To = new[] { prepared.To },
            Subject = prepared.Subject,
            Body = prepared.Body,
            IsHtml = prepared.IsHtml,
            TemplateCode = prepared.TemplateCode,
            Attachments = prepared.Attachments,
        });

        if (sentEmail is not null) { sentEmail.Status = "SENT"; sentEmail.SentAt = now; }
        if (recipient is not null) { recipient.DeliveryStatus = "SENT"; recipient.SentAt = now; }
        await _db.SaveChangesAsync(cancellationToken);

        return EmailDeliveryResult.Sent();
    }

    public async Task<SystemEmailDispatchResult> SendAsync(
        SystemEmailRequest request, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, cancellationToken);
        var delivery = await DeliverAsync(prepared, cancellationToken);
        return new SystemEmailDispatchResult(delivery, prepared.SentEmailId, prepared.EmailTemplateId);
    }
}
