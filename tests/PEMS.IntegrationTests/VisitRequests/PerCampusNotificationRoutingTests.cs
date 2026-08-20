using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.UpdatePendingVisitInstanceV2;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// WHO gets told, for the per-campus edit and the post-approval amendment (plan §18).
///
/// <para>
/// Every other suite in this area wires a <c>NoopNotifications</c> or a fake that only counts calls, so
/// "the right person was told" had never actually been asserted — a notification addressed to the wrong
/// campus's leader, or to a Host who handed the campus over last week, would pass all of them. These
/// tests wire the REAL <see cref="NotificationService"/>, let it write to <c>notifications</c>, and then
/// read the rows back: recipient, visit_instance_id and campus_id are checked against the seed's actual
/// user ids.
/// </para>
/// <para>
/// The negative half matters as much as the positive one. Routing bugs here are not loud — an extra
/// action-required notification to somebody with no action is the kind of noise that teaches people to
/// ignore the channel, and it never fails a test that only asks "was a notification sent".
/// </para>
/// </summary>
public sealed class PerCampusNotificationRoutingTests
{
    private static string ConnString => TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString(
        "server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");

    // Canonical seed. Campus 1 = HN, 2 = HCM.
    private const ulong Registrant = 8;     // VISITOR
    private const ulong LeaderHn = 3;       // STAFF/LEADER campus 1 — CoordinatorUserId of an HN instance
    private const ulong LeaderHcm = 9;      // STAFF/LEADER campus 2
    private const ulong IcStaffHn = 101;    // STAFF/STAFF campus 1 — eligible Host for HN
    private const ulong IcStaffHn2 = 102;   // STAFF/STAFF campus 1 — the Host a transfer hands HN to
    private const ulong IcStaffHcm = 103;   // STAFF/STAFF campus 2 — Host of the sibling
    private const ulong CampusHn = 1;
    private const ulong CampusHcm = 2;

    private static bool? _dbUp;
    private static readonly DateTime Now = DateTime.Now;

    private static ApplicationDbContext NewContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(ConnString, ServerVersion.AutoDetect(ConnString)).Options);

    private static void RequireDb()
    {
        if (_dbUp is null)
        {
            try { using var db = NewContext(); _dbUp = db.Database.CanConnect(); }
            catch { _dbUp = false; }
        }
        Assert.True(_dbUp!.Value, "pems_pr3_test is not reachable.");
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public FakeUser(ulong id, string roleCode = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
        {
            UserId = id; RoleCode = roleCode; SubRole = subRole; PrimaryCampusId = campusId;
        }
        public bool IsAuthenticated => true;
        public ulong? UserId { get; }
        public string? Email => null;
        public ulong? RoleId => null;
        public string? RoleCode { get; }
        public string? SubRole { get; }
        public ulong? PrimaryCampusId { get; }
        public ulong? DepartmentId => null;
        public ulong? SessionId => null;
        public string? LoginPortal => null;
    }

    private static FakeUser Leader(ulong id, ulong campusId) => new(id, RoleCodes.Staff, UserSubRoles.Leader, campusId);
    private static FakeUser Staff(ulong id, ulong campusId) => new(id, RoleCodes.Staff, UserSubRoles.Staff, campusId);

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    /// <summary>The real service, so what these tests read is what production writes.</summary>
    private static INotificationService Notifier(ApplicationDbContext db) => new NotificationService(db);

    private static UpdatePendingVisitInstanceV2CommandHandler EditHandler(ApplicationDbContext db, FakeUser actor)
        => new(db, actor, new FixedClock(),
            new VisitRequestV2EditService(db, new VisitRequestAggregateStatusService(db)),
            new CampusApprovalExecutor(
                db, new VisitRequestAggregateStatusService(db), new MySqlUserMutationLockService(db),
                Notifier(db),
                new PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService(
                    db, actor,
                    NullLogger<PEMS.Application.Delegations.Services.VisitFormRead.VisitFormReadService>.Instance,
                    new FixedClock()),
                NullLogger<CampusApprovalExecutor>.Instance),
            Notifier(db),
            NullLogger<UpdatePendingVisitInstanceV2CommandHandler>.Instance,
            ReadOn, WriteOn);

    private static SubmitVisitAmendmentCommandHandler SubmitHandler(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(),
            new VisitAmendmentService(db, NullLogger<VisitAmendmentService>.Instance),
            Notifier(db), NullLogger<SubmitVisitAmendmentCommandHandler>.Instance, WriteOn);

    // ── What a notification row says, reduced to the parts routing is about ──────────────────────

    private sealed record Sent(
        ulong Recipient, ulong? InstanceId, ulong? CampusId, string Type, bool ActionRequired,
        string? ActionType, string? MetadataJson);

    private static async Task<List<Sent>> SentForAsync(ulong requestId)
    {
        using var db = NewContext();
        return await db.Notifications.AsNoTracking()
            .Where(n => n.VisitRequestId == requestId)
            .OrderBy(n => n.NotificationId)
            .Select(n => new Sent(
                n.RecipientUserId, n.VisitInstanceId, n.CampusId, n.NotificationType, n.IsActionRequired,
                n.ActionType, n.MetadataJson))
            .ToListAsync();
    }

    // ── Fixture ─────────────────────────────────────────────────────────────────────────────────

    private static CampusVisitFormDto Campus(string code, DateTime start, string delegation = "Đoàn Notify")
        => new(code, start, start.AddMinutes(120), delegation, "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // Registrant's own address: every campus self-matches its contact at submit, so the request
            // clears the confirmation gate immediately and each campus lands on WAITING_REQUEST_APPROVAL.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "EN", null, "DECLINED", null, null);

    /// <summary>
    /// A committed HN+HCM request, both campuses waiting for their decision.
    ///
    /// <para>
    /// <paramref name="registrantUserId"/> defaults to the VISITOR who also holds both campuses as their
    /// contact — the ordinary shape. A caller passing an INTERNAL registrant gets a request whose contact
    /// is still that visitor, because the contact role is external only: the campuses are then confirmed
    /// directly, which is what the invited guest's acceptance would have done.
    /// </para>
    /// </summary>
    private static async Task<(ulong RequestId, ulong Hn, ulong Hcm)> CreatePendingAsync(
        ulong? registrantUserId = null)
    {
        var registrant = registrantUserId ?? Registrant;
        var start = Now.AddDays(20);
        ulong requestId, hn, hcm;
        using (var db = NewContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var created = await new VisitRequestV2CreateService(db).CreateV2Async(
                new VisitRequestFormDataV2(
                    Guid.NewGuid().ToString("N"),
                    new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(registrant)),
                    null, new List<CampusVisitFormDto> { Campus("HN", start), Campus("HCM", start.AddDays(1)) }),
                registrant, "VISITOR_SUBMITTED", Now, CancellationToken.None);
            await tx.CommitAsync();

            requestId = created.VisitRequestId;
            hn = created.CampusInstances.Single(c => c.CampusId == CampusHn).VisitInstanceId;
            hcm = created.CampusInstances.Single(c => c.CampusId == CampusHcm).VisitInstanceId;
        }

        if (registrant != Registrant)
            await ConfirmContactsAsync(requestId);

        return (requestId, hn, hcm);
    }

    /// <summary>The invited guest accepts every campus, so the request clears the confirmation gate.</summary>
    private static async Task ConfirmContactsAsync(ulong requestId)
    {
        using var db = NewContext();
        foreach (var instance in await db.VisitRequestCampuses.Where(c => c.VisitRequestId == requestId).ToListAsync())
        {
            instance.OperationalContactUserId = Registrant;      // the VISITOR named as contact
            instance.OperationalContactConfirmedAt = Now;
            instance.OperationalContactConfirmationSource = OperationalContactSources.EmailConfirmation;
            instance.Status = VisitInstanceStatuses.WaitingRequestApproval;
        }

        var visit = await db.VisitRequests.SingleAsync(v => v.VisitRequestId == requestId);
        visit.Status = VisitRequestStatuses.PendingApproval;
        visit.ContactGateRevision = 1;

        foreach (var invitation in await db.VisitRequestIdentityChanges
                     .Where(c => c.VisitRequestId == requestId && c.Status == IdentityChangeStatuses.Pending)
                     .ToListAsync())
        {
            invitation.Status = IdentityChangeStatuses.Applied;
            invitation.NewUserId = Registrant;
            invitation.AppliedAt = Now;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The same request, driven to APPROVED with a NAMED Host per campus — deliberately not the campus
    /// leader, because "the Host was told and the leader was not" is unprovable when they are one person.
    /// Campuses are decided first and the parent flipped after, which is the only order the triggers allow.
    /// </summary>
    private static async Task<(ulong RequestId, ulong Hn, ulong Hcm)> CreateApprovedAsync()
    {
        var (requestId, hn, hcm) = await CreatePendingAsync();

        using (var db = NewContext())
        {
            var visit = await db.VisitRequests.Include(v => v.CampusInstances)
                .SingleAsync(v => v.VisitRequestId == requestId);

            foreach (var instance in visit.CampusInstances)
            {
                var isHn = instance.CampusId == CampusHn;
                instance.Status = VisitInstanceStatuses.Assigned;
                instance.CurrentHostUserId = isHn ? IcStaffHn : IcStaffHcm;
                instance.HostAssignedBy = isHn ? LeaderHn : LeaderHcm;   // must be the campus's leader
                instance.HostAssignedAt = Now;
                instance.DecidedBy = isHn ? LeaderHn : LeaderHcm;
                instance.DecidedAt = Now;
                instance.DecisionActorRole = "STAFF_LEADER";
                instance.DecisionSource = "STANDARD_CAMPUS_REVIEW";
                instance.RowVersion += 1;
            }
            await db.SaveChangesAsync();

            visit.Status = VisitRequestStatuses.Approved;
            visit.RowVersion += 1;
            await db.SaveChangesAsync();
        }

        return (requestId, hn, hcm);
    }

    /// <summary>A per-campus edit payload that really differs, echoing back the immutable contact.</summary>
    private static async Task<CampusVisitEditV2Dto> PayloadAsync(ulong instanceId, string delegation)
    {
        using var db = NewContext();
        var i = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceId);
        var d = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(x => x.VisitInstanceId == instanceId);
        return new CampusVisitEditV2Dto(
            instanceId, i.RowVersion, i.CampusId == CampusHn ? "HN" : "HCM",
            i.PlannedStartAt, i.PlannedEndAt,
            delegation, d.VisitType ?? "MEETING", d.VisitTypeOther, d.Purpose ?? "Thăm", d.WorkingContent,
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            new ContactPointDto(d.OperationalContactFullName ?? "Op Contact", d.OperationalContactOrganization ?? "OpOrg",
                "Trưởng phòng Hợp tác", d.OperationalContactPhone ?? "+8410", d.OperationalContactEmail ?? ""),
            d.WorkingLanguage ?? "EN", d.TransportationNote, d.MediaConsentStatus ?? "DECLINED", d.Notes);
    }

    private static async Task<VisitAmendmentProposalDto> ProposalAsync(ulong instanceId, string purpose)
    {
        using var db = NewContext();
        var d = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(x => x.VisitInstanceId == instanceId);
        var i = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(x => x.VisitInstanceId == instanceId);
        var linkIds = await db.VisitInstanceGuestMembers.AsNoTracking()
            .Where(l => l.VisitInstanceId == instanceId).Select(l => l.GuestMemberId).ToListAsync();
        var members = await db.VisitGuestMembers.AsNoTracking()
            .Where(m => linkIds.Contains(m.GuestMemberId)).OrderBy(m => m.DisplayOrder).ToListAsync();

        return new VisitAmendmentProposalDto(
            i.RowVersion, d.FormRevision, d.ApprovalRevision, "Cập nhật theo yêu cầu đoàn.",
            d.DelegationName, d.VisitType ?? "MEETING", d.VisitTypeOther, purpose, d.WorkingContent,
            d.WorkingLanguage ?? "EN",
            new ContactPointDto(d.OperationalContactFullName ?? "", d.OperationalContactOrganization ?? "",
                "Trưởng phòng Hợp tác", d.OperationalContactPhone ?? "", d.OperationalContactEmail ?? ""),
            members.Where(m => m.MemberType == "GUEST")
                .Select(m => new VisitorDto(m.FullName, m.Nationality ?? "", m.JobTitle ?? "", m.Organization ?? "")).ToList(),
            new List<SupportTeamMemberDto>(),
            i.PlannedStartAt, i.PlannedEndAt);
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, requestId);
        await Del("DELETE FROM notifications WHERE visit_request_id = {0}");
        await Del("DELETE ac FROM visit_instance_amendment_changes ac JOIN visit_instance_amendments a ON a.amendment_id = ac.amendment_id WHERE a.visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_amendments WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_participants WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_identity_change_events WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_identity_changes WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_request_revision_history WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_guest_members WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_form_details WHERE visit_instance_id IN (SELECT visit_instance_id FROM visit_request_campuses WHERE visit_request_id = {0})");
        await Del("DELETE FROM visit_request_campuses WHERE visit_request_id = {0}");
        await Del("DELETE alc FROM audit_log_changes alc JOIN audit_logs al ON al.audit_log_id = alc.audit_log_id WHERE al.visit_request_id = {0}");
        await Del("DELETE FROM audit_logs WHERE visit_request_id = {0}");
        await Del("DELETE FROM visit_requests WHERE visit_request_id = {0}");
    }

    // ── 1. Per-campus edit tells THIS campus's leader, and nobody else's ─────────────────────────

    /// <summary>
    /// Editing HCM is news for the person who has to decide HCM. The leader of HN has nothing to
    /// reprocess — their campus did not move — and telling them anyway is how a two-campus request
    /// produces two action-required notifications for one action.
    /// </summary>
    [Fact]
    public async Task Editing_one_campus_notifies_only_that_campus_leader()
    {
        RequireDb();
        var requestId = 0UL;
        try
        {
            (requestId, var hn, var hcm) = await CreatePendingAsync();

            using (var db = NewContext())
                await EditHandler(db, new FakeUser(Registrant)).Handle(
                    new UpdatePendingVisitInstanceV2Command(
                        requestId, hcm, await PayloadAsync(hcm, "Đoàn HCM đã sửa"), false, null),
                    CancellationToken.None);

            var sent = await SentForAsync(requestId);

            // Exactly one, and it is addressed to HCM's Staff Leader about the HCM instance.
            var one = Assert.Single(sent);
            Assert.Equal(LeaderHcm, one.Recipient);
            Assert.Equal(hcm, one.InstanceId);
            Assert.Equal(CampusHcm, one.CampusId);
            Assert.True(one.ActionRequired);

            // The sibling's leader is not told, and the notification does not carry the sibling's id.
            Assert.DoesNotContain(sent, s => s.Recipient == LeaderHn);
            Assert.DoesNotContain(sent, s => s.InstanceId == hn);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Producer contract (plan PEMS_FIX_NOTIFICATION_SEMANTIC_ROUTING_SYSTEM_WIDE.md §6/§7/§14): this
    /// is the exact producer behind "Thông tin cơ sở chờ duyệt đã được cập nhật" — the reported bug was
    /// this notification opening the live "Duyệt &amp; phân công người phụ trách" modal. A plain per-
    /// campus save (no <c>ApproveAfterSave</c>) means "data changed, go read it", never "a decision is
    /// waiting" — its eventKey must classify as VISIT_HISTORY on the frontend, and its actionType must
    /// say so explicitly rather than relying on the coarse OPEN_VISIT_DETAIL fallback. This test fails
    /// the moment a future change points either field at review/approval instead.
    /// </summary>
    [Fact]
    public async Task Pending_campus_edit_notification_is_tagged_VISIT_REQUEST_UPDATED_PENDING_never_review()
    {
        RequireDb();
        var requestId = 0UL;
        try
        {
            (requestId, _, var hcm) = await CreatePendingAsync();

            using (var db = NewContext())
                await EditHandler(db, new FakeUser(Registrant)).Handle(
                    new UpdatePendingVisitInstanceV2Command(
                        requestId, hcm, await PayloadAsync(hcm, "Đoàn HCM đã sửa lần nữa"), false, null),
                    CancellationToken.None);

            var one = Assert.Single(await SentForAsync(requestId));

            Assert.Equal(NotificationActionTypes.OpenVisitHistory, one.ActionType);
            Assert.NotEqual(NotificationActionTypes.OpenCampusReview, one.ActionType);

            Assert.NotNull(one.MetadataJson);
            using var meta = System.Text.Json.JsonDocument.Parse(one.MetadataJson!);
            var eventKey = meta.RootElement.GetProperty("eventKey").GetString();
            Assert.Equal(NotificationEventKeys.VisitRequestUpdatedPending, eventKey);
            Assert.NotEqual(NotificationEventKeys.VisitRequestWaitingApproval, eventKey);
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The skip is deliberate, not an accident of routing: <c>NotifyLeaderAsync</c> returns early when the
    /// leader IS the editor. "You changed something" is not news to the person who changed it.
    ///
    /// <para>
    /// The editor is a leader who FILED the request, which is now the only way a Staff Leader reaches a
    /// pending-campus edit at all — a leader may no longer rewrite a request somebody else submitted,
    /// they decide it. The invariant under test is unchanged; only the one actor who can still trigger
    /// it is.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_leader_editing_a_campus_of_their_own_request_is_not_notified_about_it()
    {
        RequireDb();
        var requestId = 0UL;
        try
        {
            (requestId, _, var hcm) = await CreatePendingAsync(registrantUserId: LeaderHcm);

            using (var db = NewContext())
                await EditHandler(db, Leader(LeaderHcm, CampusHcm)).Handle(
                    new UpdatePendingVisitInstanceV2Command(
                        requestId, hcm, await PayloadAsync(hcm, "Leader HCM tự sửa"), false, null),
                    CancellationToken.None);

            Assert.Empty(await SentForAsync(requestId));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── 2. An amendment is decided by the Host, so the Host is who gets told ─────────────────────

    /// <summary>
    /// §18 — the proposal goes to the campus's CURRENT HOST. Not the Staff Leader who approved the campus
    /// (they no longer decide amendments, so an action-required notification to them is a lie about what
    /// they can do), and not the sibling campus's Host, who has their own day to prepare.
    /// </summary>
    [Fact]
    public async Task A_submitted_amendment_notifies_the_current_host_and_not_the_leader_or_the_sibling()
    {
        RequireDb();
        var requestId = 0UL;
        try
        {
            (requestId, var hn, var hcm) = await CreateApprovedAsync();

            using (var db = NewContext())
                await SubmitHandler(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, hn, await ProposalAsync(hn, "Đổi mục đích")),
                    CancellationToken.None);

            var sent = await SentForAsync(requestId);

            var one = Assert.Single(sent);
            Assert.Equal(IcStaffHn, one.Recipient);       // the Host of HN
            Assert.Equal(hn, one.InstanceId);
            Assert.Equal(CampusHn, one.CampusId);
            Assert.True(one.ActionRequired);

            Assert.DoesNotContain(sent, s => s.Recipient == LeaderHn);      // approved it; does not decide this
            Assert.DoesNotContain(sent, s => s.Recipient == IcStaffHcm);    // sibling's Host
            Assert.DoesNotContain(sent, s => s.InstanceId == hcm);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── 3. Nobody is told to review their own change ─────────────────────────────────────────────

    /// <summary>
    /// When the person filing the proposal is the campus's Host, it is approved in the same call — so
    /// there is no pending decision and nothing to notify. "You have a proposal waiting for you" sent to
    /// the person who just approved it is the exact message §18 forbids.
    /// </summary>
    [Fact]
    public async Task A_self_approved_amendment_notifies_nobody()
    {
        RequireDb();
        var requestId = 0UL;
        try
        {
            (requestId, var hn, _) = await CreateApprovedAsync();

            // Give HN's Host the requester side of that campus, by making them its operational contact.
            // (The other direction — registrant becomes Host — the schema refuses: a Host must be IC
            // Staff of the campus, and the registrant here is a VISITOR.)
            using (var db = NewContext())
            {
                var a = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == hn);
                a.OperationalContactUserId = IcStaffHn;
                a.OperationalContactConfirmedAt = Now;
                a.OperationalContactConfirmationSource = OperationalContactSources.Transfer;
                a.RowVersion += 1;
                await db.SaveChangesAsync();
            }

            using (var db = NewContext())
            {
                var dto = await SubmitHandler(db, IcStaffHn).Handle(
                    new SubmitVisitAmendmentCommand(requestId, hn, await ProposalAsync(hn, "Tự cập nhật")),
                    CancellationToken.None);
                Assert.Equal(AmendmentStatuses.Approved, dto.Status);
            }

            // No notification at all — in particular none addressed to the actor themself.
            Assert.Empty(await SentForAsync(requestId));

            // And nothing is left waiting, so no later job can produce the reminder either.
            using var check = NewContext();
            Assert.False(await check.VisitInstanceAmendments.AsNoTracking()
                .AnyAsync(a => a.VisitInstanceId == hn && a.Status == AmendmentStatuses.PendingApproval));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── 4. Authority — and therefore the address — travels with the Host role ────────────────────

    /// <summary>
    /// After HN is handed from A to B, the next proposal is B's to decide, so B is who hears about it.
    /// The recipient is read at SUBMIT time from <c>current_host_user_id</c> rather than from anything
    /// captured earlier, which is what makes a handover take effect immediately instead of at the next
    /// approval.
    /// </summary>
    [Fact]
    public async Task After_a_host_transfer_the_new_host_is_notified_and_the_old_one_is_not()
    {
        RequireDb();
        var requestId = 0UL;
        try
        {
            (requestId, var hn, _) = await CreateApprovedAsync();

            // The handover: HN goes from 101 to 102, both IC Staff of campus 1.
            using (var db = NewContext())
            {
                var a = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == hn);
                Assert.Equal(IcStaffHn, a.CurrentHostUserId);   // the state the transfer starts from
                a.CurrentHostUserId = IcStaffHn2;
                a.HostAssignedBy = LeaderHn;
                a.HostAssignedAt = Now;
                a.RowVersion += 1;
                await db.SaveChangesAsync();
            }

            using (var db = NewContext())
                await SubmitHandler(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, hn, await ProposalAsync(hn, "Đổi sau bàn giao")),
                    CancellationToken.None);

            var sent = await SentForAsync(requestId);

            var one = Assert.Single(sent);
            Assert.Equal(IcStaffHn2, one.Recipient);
            Assert.Equal(hn, one.InstanceId);
            Assert.Equal(CampusHn, one.CampusId);

            // The previous Host is not told about a decision that is no longer theirs.
            Assert.DoesNotContain(sent, s => s.Recipient == IcStaffHn);
            Assert.DoesNotContain(sent, s => s.Recipient == LeaderHn);
        }
        finally { await CleanupAsync(requestId); }
    }
}
