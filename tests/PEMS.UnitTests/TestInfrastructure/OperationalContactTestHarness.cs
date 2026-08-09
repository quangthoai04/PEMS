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
/// EF Core InMemory stand-in for <see cref="IApplicationDbContext"/> covering the operational-contact
/// invitation slice: a request, its campuses and their form details, the identity changes that invite
/// somebody to a campus, the events and audit rows those writes produce, and the email-action tokens
/// that answer them.
///
/// <para>
/// It exists as its own slice rather than as a widening of <c>DelegationsTestDbContext</c>, which
/// prunes <see cref="VisitRequestIdentityChange"/> outright — the tests here are about that very
/// aggregate, so it has to be in the model.
/// </para>
/// <para>
/// InMemory has NO transactions (the warning is suppressed and <c>BeginTransactionAsync</c> returns a
/// no-op). Nothing in this file may therefore be read as evidence about rollback: what it can show is
/// the ORDER a handler does things in — mint, then save, then send — and what it does when a step
/// fails. Whether a failed mint actually un-does the writes before it is a property of the database
/// and is only provable against one.
/// </para>
/// </summary>
public class OperationalContactTestDbContext : DbContext, IApplicationDbContext
{
    /// <summary>Every SaveChanges this context performs, appended to the shared journal in order.</summary>
    public List<string> Journal { get; } = new();

    public static OperationalContactTestDbContext Create() =>
        new(new DbContextOptionsBuilder<OperationalContactTestDbContext>()
            .UseInMemoryDatabase($"pems-opcontact-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public OperationalContactTestDbContext(DbContextOptions<OperationalContactTestDbContext> options)
        : base(options) { }

    // ── The aggregates these handlers actually touch ─────────────────────────
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Campus> Campuses => Set<Campus>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<VisitRequest> VisitRequests => Set<VisitRequest>();
    public DbSet<VisitRequestCampus> VisitRequestCampuses => Set<VisitRequestCampus>();
    public DbSet<VisitInstanceFormDetail> VisitInstanceFormDetails => Set<VisitInstanceFormDetail>();
    public DbSet<VisitRequestIdentityChange> VisitRequestIdentityChanges => Set<VisitRequestIdentityChange>();
    public DbSet<VisitRequestIdentityChangeEvent> VisitRequestIdentityChangeEvents => Set<VisitRequestIdentityChangeEvent>();
    public DbSet<EmailActionToken> EmailActionTokens => Set<EmailActionToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UploadedFile> Files => Set<UploadedFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Prune everything reachable from the slice above that these handlers never read. The rule is
        // the same one the other slices use: map what the handler touches, ignore the rest, so a
        // missing table is a compile-time decision rather than a runtime surprise.
        modelBuilder.Ignore<UserAuthProvider>();
        modelBuilder.Ignore<UserSession>();
        modelBuilder.Ignore<AccountEmailConfirmation>();
        modelBuilder.Ignore<OtpToken>();
        modelBuilder.Ignore<LoginLog>();
        modelBuilder.Ignore<SecurityEvent>();
        modelBuilder.Ignore<Partner>();
        modelBuilder.Ignore<PartnerContact>();
        modelBuilder.Ignore<PartnerAlias>();
        modelBuilder.Ignore<VisitGuestPartnerLink>();
        modelBuilder.Ignore<VisitGuestMember>();
        modelBuilder.Ignore<VisitInstanceGuestMember>();
        modelBuilder.Ignore<VisitParticipant>();
        modelBuilder.Ignore<VisitLogisticsItem>();
        modelBuilder.Ignore<VisitLogisticsItemHandover>();
        modelBuilder.Ignore<VisitLogisticsAssignmentAttempt>();
        modelBuilder.Ignore<VisitInstanceReminderSetting>();
        modelBuilder.Ignore<VisitExpenseReport>();
        modelBuilder.Ignore<VisitExpenseItem>();
        modelBuilder.Ignore<VisitExpenseReportEvent>();
        modelBuilder.Ignore<VisitPhotoFolder>();
        modelBuilder.Ignore<VisitPhoto>();
        modelBuilder.Ignore<PhotoFaceTag>();
        modelBuilder.Ignore<VisitAgenda>();
        modelBuilder.Ignore<VisitInstanceAmendment>();
        modelBuilder.Ignore<VisitInstanceAmendmentChange>();
        modelBuilder.Ignore<VisitInstanceFormRevisionHistory>();
        modelBuilder.Ignore<VisitRequestRevisionHistory>();
        modelBuilder.Ignore<CalendarEvent>();
        modelBuilder.Ignore<CalendarEventAttendee>();
        modelBuilder.Ignore<CalendarEventReminder>();
        modelBuilder.Ignore<Notification>();
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
        modelBuilder.Ignore<SentEmail>();
        modelBuilder.Ignore<SentEmailRecipient>();
        modelBuilder.Ignore<SentEmailAttachment>();
        modelBuilder.Ignore<EmailTemplate>();
        modelBuilder.Ignore<ApiConfiguration>();
        modelBuilder.Ignore<ApiUsageQuota>();
        modelBuilder.Ignore<ApiRequestLog>();
        modelBuilder.Ignore<BusinessCardOcrJob>();
        modelBuilder.Ignore<AgendaTemplate>();
        modelBuilder.Ignore<AgendaTemplateItem>();
        modelBuilder.Ignore<AgendaTemplateDefault>();
        // Audit CHANGES are mapped: replace records the old/new contact as field-level rows, and a
        // test that could not see them could not tell a masked audit from a missing one.

        // ── Same pairings as the production ApplicationDbContext (ambiguous by convention) ──
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
        modelBuilder.Entity<VisitRequestCampus>()
            .HasAlternateKey(vc => new { vc.VisitRequestId, vc.VisitInstanceId });
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.ActorUser).WithMany().HasForeignKey(a => a.ActorUserId);
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.RecipientUser).WithMany().HasForeignKey(t => t.RecipientUserId);
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.SentEmail).WithMany().HasForeignKey(t => t.SentEmailId);
        modelBuilder.Entity<EmailActionToken>()
            .HasOne(t => t.SentEmailRecipient).WithMany().HasForeignKey(t => t.SentEmailRecipientId);

        // ── The identity-change aggregate, wired exactly as production does ──
        modelBuilder.Entity<VisitRequestIdentityChange>(b =>
        {
            b.HasOne(c => c.CampusInstance).WithMany(vc => vc.IdentityChanges)
                .HasForeignKey(c => new { c.VisitRequestId, c.VisitInstanceId })
                .HasPrincipalKey(vc => new { vc.VisitRequestId, vc.VisitInstanceId });
            b.HasOne<User>().WithMany().HasForeignKey(c => c.OldUserId);
            b.HasOne<User>().WithMany().HasForeignKey(c => c.NewUserId);
            b.HasOne<User>().WithMany().HasForeignKey(c => c.RequestedBy);
        });
        modelBuilder.Entity<VisitRequestIdentityChangeEvent>(b =>
        {
            b.HasOne(e => e.IdentityChange).WithMany(c => c.Events)
                .HasForeignKey(e => e.IdentityChangeId);
            b.HasOne<User>().WithMany().HasForeignKey(e => e.ActorUserId);
        });
    }

    /// <summary>
    /// Journals every flush. The point of the journal is ORDER: an invitation's links have to be part
    /// of the same save as the change they answer, and the email has to come after it — assertions
    /// about "atomic" that cannot see when the writes happened are not assertions about atomicity.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Journal.Add("save");
        return base.SaveChangesAsync(cancellationToken);
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
    DbSet<VisitInstanceGuestMember> IApplicationDbContext.VisitInstanceGuestMembers => Set<VisitInstanceGuestMember>();
    DbSet<VisitParticipant> IApplicationDbContext.VisitParticipants => Set<VisitParticipant>();
    DbSet<VisitAgenda> IApplicationDbContext.VisitAgendas => Set<VisitAgenda>();
    DbSet<VisitLogisticsItem> IApplicationDbContext.VisitLogisticsItems => Set<VisitLogisticsItem>();
    DbSet<VisitLogisticsItemHandover> IApplicationDbContext.VisitLogisticsItemHandovers => Set<VisitLogisticsItemHandover>();
    DbSet<VisitLogisticsAssignmentAttempt> IApplicationDbContext.VisitLogisticsAssignmentAttempts => Set<VisitLogisticsAssignmentAttempt>();
    DbSet<VisitInstanceReminderSetting> IApplicationDbContext.VisitInstanceReminderSettings => Set<VisitInstanceReminderSetting>();
    DbSet<VisitExpenseReport> IApplicationDbContext.VisitExpenseReports => Set<VisitExpenseReport>();
    DbSet<VisitExpenseItem> IApplicationDbContext.VisitExpenseItems => Set<VisitExpenseItem>();
    DbSet<VisitExpenseReportEvent> IApplicationDbContext.VisitExpenseReportEvents => Set<VisitExpenseReportEvent>();
    DbSet<VisitPhotoFolder> IApplicationDbContext.VisitPhotoFolders => Set<VisitPhotoFolder>();
    DbSet<VisitPhoto> IApplicationDbContext.VisitPhotos => Set<VisitPhoto>();
    DbSet<VisitInstanceAmendment> IApplicationDbContext.VisitInstanceAmendments => Set<VisitInstanceAmendment>();
    DbSet<VisitInstanceAmendmentChange> IApplicationDbContext.VisitInstanceAmendmentChanges => Set<VisitInstanceAmendmentChange>();
    DbSet<VisitInstanceFormRevisionHistory> IApplicationDbContext.VisitInstanceFormRevisionHistories => Set<VisitInstanceFormRevisionHistory>();
    DbSet<VisitRequestRevisionHistory> IApplicationDbContext.VisitRequestRevisionHistories => Set<VisitRequestRevisionHistory>();
    DbSet<VisitRequestPendingForm> IApplicationDbContext.VisitRequestPendingForms => Set<VisitRequestPendingForm>();
    DbSet<VisitRequestFingerprintGuard> IApplicationDbContext.VisitRequestFingerprintGuards => Set<VisitRequestFingerprintGuard>();
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
    DbSet<EmailSendIdempotency> IApplicationDbContext.EmailSendIdempotencies => Set<EmailSendIdempotency>();
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

    /// <summary>
    /// A no-op InMemory transaction, wrapped so the journal records where its boundaries fall.
    ///
    /// <para>
    /// This is the load-bearing part of the harness. "Mint happened, then a save, then an email" is
    /// true of BOTH the broken code and the fixed code — the old wrapper minted, saved and sent in that
    /// order too, just after somebody else's commit. The only thing that tells the two apart is which
    /// SIDE of the commit the mint fell on, so the boundary has to be visible.
    /// </para>
    /// </summary>
    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        Journal.Add("tx-begin");
        return WrapAsync(cancellationToken);

        async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> WrapAsync(CancellationToken ct)
            => new JournallingTransaction(await Database.BeginTransactionAsync(ct), Journal);
    }

    private sealed class JournallingTransaction : Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction
    {
        private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _inner;
        private readonly List<string> _journal;

        public JournallingTransaction(
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction inner, List<string> journal)
        {
            _inner = inner;
            _journal = journal;
        }

        public Guid TransactionId => _inner.TransactionId;

        public void Commit() { _journal.Add("tx-commit"); _inner.Commit(); }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            _journal.Add("tx-commit");
            return _inner.CommitAsync(cancellationToken);
        }

        public void Rollback() { _journal.Add("tx-rollback"); _inner.Rollback(); }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            _journal.Add("tx-rollback");
            return _inner.RollbackAsync(cancellationToken);
        }

        public void Dispose() => _inner.Dispose();

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }
}

/// <summary>
/// The invitation service, recorded. It stands in for the Infrastructure one — which the unit-test
/// project cannot reference — and keeps the two behaviours the handlers depend on: the lock returns
/// the campus's single PENDING change (tracked, so a handler's edits to it are visible), and the mint
/// ADDS token rows to the caller's context without saving them, exactly as the real one does.
///
/// <para>
/// Everything it is asked to do lands in <see cref="Journal"/>, the same list the context writes its
/// saves into, so a test can assert on the ORDER of mint / save / dispatch rather than merely on the
/// fact that each happened.
/// </para>
/// </summary>
public sealed class RecordingOperationalContactInvitationService : IOperationalContactInvitationService
{
    private readonly OperationalContactTestDbContext _db;
    private readonly DateTime _now;

    public RecordingOperationalContactInvitationService(OperationalContactTestDbContext db, DateTime now)
    {
        _db = db;
        _now = now;
    }

    public List<string> Journal => _db.Journal;

    /// <summary>Token version seen at MINT time, per identity change, in call order.</summary>
    public List<(ulong ChangeId, uint TokenVersion)> Mints { get; } = new();

    /// <summary>Identity changes an email was dispatched for, in call order.</summary>
    public List<ulong> Dispatches { get; } = new();

    /// <summary>When set, the mint throws instead — the token-persistence failure of the plan.</summary>
    public Exception? FailMintWith { get; set; }

    /// <summary>When set, the dispatch throws — a mail-provider outage AFTER the links are durable.</summary>
    public Exception? FailDispatchWith { get; set; }

    public async Task<OperationalContactInvitationTokens?> MintInvitationTokensAsync(
        ulong identityChangeId, CancellationToken cancellationToken)
    {
        if (FailMintWith is not null)
        {
            Journal.Add($"mint-failed:{identityChangeId}");
            throw FailMintWith;
        }

        var change = await _db.VisitRequestIdentityChanges
            .FirstOrDefaultAsync(c => c.IdentityChangeId == identityChangeId, cancellationToken);
        if (change is null
            || change.Status != IdentityChangeStatuses.Pending
            || string.IsNullOrWhiteSpace(change.NewEmailNormalized))
            return null;

        Journal.Add($"mint:{identityChangeId}:v{change.TokenVersion}");
        Mints.Add((identityChangeId, change.TokenVersion));

        // One row per answer, keyed by version — the shape the real service produces, and NOT saved:
        // the caller's own commit is what makes them durable alongside the change.
        var groupKey = $"OP_CONTACT_CONFIRM:{identityChangeId}:{change.TokenVersion}";
        foreach (var action in new[] { EmailIntendedActions.Accept, EmailIntendedActions.Decline })
            _db.EmailActionTokens.Add(new EmailActionToken
            {
                TokenHash = $"hash-{identityChangeId}-{change.TokenVersion}-{action}",
                ActionContext = change.ChangeKind == IdentityChangeKinds.Transfer
                    ? EmailActionContexts.VisitContactTransfer
                    : EmailActionContexts.VisitContactClaim,
                ActionGroupKey = groupKey,
                TargetType = EmailActionTargetTypes.VisitRequestIdentityChange,
                TargetId = identityChangeId,
                IntendedAction = action,
                RecipientUserId = null,
                RecipientEmail = change.NewEmailNormalized!,
                ExpiresAt = change.ExpiresAt,
                ResultStatus = EmailActionResultStatuses.Pending,
                CreatedAt = _now,
            });

        return new OperationalContactInvitationTokens(
            $"accept-{identityChangeId}-v{change.TokenVersion}",
            $"decline-{identityChangeId}-v{change.TokenVersion}");
    }

    public Task DispatchInvitationEmailAsync(
        ulong identityChangeId, OperationalContactInvitationTokens tokens, CancellationToken cancellationToken)
    {
        Journal.Add($"dispatch:{identityChangeId}");
        Dispatches.Add(identityChangeId);
        return FailDispatchWith is null ? Task.CompletedTask : Task.FromException(FailDispatchWith);
    }

    public async Task<VisitRequestIdentityChange?> LockChangeAsync(
        ulong identityChangeId, CancellationToken cancellationToken)
        => await _db.VisitRequestIdentityChanges
            .FirstOrDefaultAsync(c => c.IdentityChangeId == identityChangeId, cancellationToken);

    public async Task<VisitRequestIdentityChange?> LockPendingChangeForInstanceAsync(
        ulong visitInstanceId, CancellationToken cancellationToken)
        => await _db.VisitRequestIdentityChanges.FirstOrDefaultAsync(
            c => c.VisitInstanceId == visitInstanceId && c.Status == IdentityChangeStatuses.Pending,
            cancellationToken);
}
