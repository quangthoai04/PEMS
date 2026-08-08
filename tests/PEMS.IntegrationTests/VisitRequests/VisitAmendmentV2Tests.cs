using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Common.DTOs;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Options;
using PEMS.Application.Delegations.Commands.CreateVisitRequestV2;
using PEMS.Application.Delegations.Commands.VisitAmendments;
using PEMS.Application.Delegations.Services;
using PEMS.Application.Notifications.Common;
using PEMS.Domain.Constants;
using PEMS.Domain.Policies;
using PEMS.Domain.Entities.Delegations;
using PEMS.Infrastructure.Persistence;
using PEMS.Infrastructure.Services;
using Xunit;

namespace PEMS.IntegrationTests.VisitRequests;

/// <summary>
/// Phase E-2 — per-campus AMENDMENTS (plan §16.6) against pems_pr3_test. Setup drives a committed
/// two-campus v2 request to APPROVED/ASSIGNED using the real transition order, then exercises
/// submit/approve/reject/withdraw/expire. Active snapshots must NEVER move before approval; approve is
/// target-only (sibling + approval status untouched). Everything is cascade-cleaned in finally.
/// </summary>
public sealed class VisitAmendmentV2Tests
{
    private static string ConnString => PEMS.IntegrationTests.TestInfrastructure.DisposableDatabaseManager.GetDisposableConnectionString("server=localhost;port=3306;database=pems_pr3_test;user=root;password=123456;AllowUserVariables=True;GuidFormat=None");
    private const ulong Registrant = 8;
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
        public FakeUser(ulong id, string role = RoleCodes.Visitor, string? subRole = null, ulong? campusId = null)
        {
            UserId = id; RoleCode = role; SubRole = subRole; PrimaryCampusId = campusId;
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

    private sealed class FixedClock : IDateTimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime VietnamNow => Now;
    }

    private sealed class NoopNotifications : INotificationService
    {
        public Task CreateManyAsync(IEnumerable<CreateNotificationRequest> requests, CancellationToken ct) => Task.CompletedTask;
        public Task CreateManyAsync(IEnumerable<CreateNotificationItem> items, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(ulong recipientUserId, string title, string? message, string notificationType, string? relatedType, ulong? relatedId, CancellationToken ct) => Task.CompletedTask;
        public Task CreateAsync(CreateNotificationRequest request, CancellationToken ct) => Task.CompletedTask;
    }

    private static readonly PerCampusFormV2Options ReadOn = new() { Enabled = true };
    private static readonly PerCampusFormV2WriteOptions WriteOn = new() { Enabled = true };

    private static SubmitVisitAmendmentCommandHandler Submit(ApplicationDbContext db, ulong actor)
        => new(db, new FakeUser(actor), new FixedClock(),
            new VisitAmendmentService(db, NullLogger<VisitAmendmentService>.Instance),
            new NoopNotifications(), NullLogger<SubmitVisitAmendmentCommandHandler>.Instance, WriteOn);

    private static DecideVisitAmendmentCommandHandlers Decide(ApplicationDbContext db, ICurrentUserService user)
        => new(db, user, new FixedClock(),
            new VisitAmendmentService(db, NullLogger<VisitAmendmentService>.Instance),
            new NoopNotifications(), NullLogger<DecideVisitAmendmentCommandHandlers>.Instance, WriteOn);

    private static CampusVisitFormDto Campus(string code, DateTime start)
        => new(code, start, start.AddMinutes(120), "Đoàn Amend", "MEETING", null, "Thăm", "Nội dung",
            new List<VisitorDto> { new("Guest A", "VN", "Guest", "GuestOrg") },
            new List<SupportTeamMemberDto>(),
            // The contact is the REGISTRANT'S own address, so the campus self-matches at submit: confirmed
            // with no invitation, and the request is past the confirmation gate from the start. This suite
            // does not test that gate, and a campus behind it can be neither decided nor moved forward.
            new ContactPointDto("Op Contact", "OpOrg", "Trưởng phòng Hợp tác", "+8410", V2SeedActor.Email(Registrant)),
            "EN", "Xe 16 chỗ", "AGREED", null, null);

    /// <summary>Creates a committed 2-campus request and drives BOTH instances to ASSIGNED (parent APPROVED)
    /// using the real transition order. Returns (requestId, instanceA=HN, instanceB=HCM).</summary>
    private static async Task<(ulong RequestId, ulong InstanceA, ulong InstanceB)> CreateApprovedAsync(DateTime start)
    {
        // FILED far out, then moved onto the date the test wants: a visit cannot be created inside
        // VisitMutationPolicy.MinScheduleLeadHours, it gets that close by the date approaching. The
        // late-window case below is about the AMENDMENT cutoff, which is a different rule.
        var filedStart = Now.AddDays(40);
        ulong requestId;
        using (var db = NewContext())
        {
            var handler = new CreateVisitRequestV2CommandHandler(
                db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
                new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
                new UserProvisionService(db),
                NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
                    new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));
            var form = new VisitRequestFormDataV2(
                "AM" + Guid.NewGuid().ToString("N"),
                new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
                null, new List<CampusVisitFormDto> { Campus("HN", filedStart), Campus("HCM", filedStart.AddDays(1)) });
            requestId = (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
        }
        using (var db = NewContext())
        {
            var visit = await db.VisitRequests.Include(v => v.CampusInstances)
                .SingleAsync(v => v.VisitRequestId == requestId);
            // Time passing, as it does — HN onto `start`, HCM a day behind it, each keeping its duration.
            var ordered = visit.CampusInstances.OrderBy(c => c.PlannedStartAt).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var duration = ordered[i].PlannedEndAt - ordered[i].PlannedStartAt;
                ordered[i].PlannedStartAt = start.AddDays(i);
                ordered[i].PlannedEndAt = ordered[i].PlannedStartAt + duration;
            }

            foreach (var instance in visit.CampusInstances)
            {
                instance.Status = VisitInstanceStatuses.Assigned;
                instance.CurrentHostUserId = instance.CoordinatorUserId; // leader self-hosts (valid seed user)
                instance.HostAssignedBy = instance.CoordinatorUserId;
                instance.HostAssignedAt = Now;
                instance.DecidedBy = instance.CoordinatorUserId;
                instance.DecidedAt = Now;
                instance.DecisionActorRole = "STAFF_LEADER";
                instance.DecisionSource = "STANDARD_CAMPUS_REVIEW";
                instance.RowVersion += 1;
            }
            await db.SaveChangesAsync(); // instances decided under the still-pending parent
            visit.Status = VisitRequestStatuses.Approved;
            visit.RowVersion += 1;
            await db.SaveChangesAsync(); // then the parent flips APPROVED
        }
        using (var db = NewContext())
        {
            var rows = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitRequestId == requestId)
                .OrderBy(c => c.CampusId)
                .Select(c => c.VisitInstanceId)
                .ToListAsync();
            return (requestId, rows[0], rows[1]);
        }
    }

    private static async Task<VisitAmendmentProposalDto> BaselineProposalAsync(ulong instanceId, Action<Baseline>? mutate = null)
    {
        using var db = NewContext();
        var d = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(x => x.VisitInstanceId == instanceId);
        var instance = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(x => x.VisitInstanceId == instanceId);
        var links = await db.VisitInstanceGuestMembers.AsNoTracking()
            .Where(l => l.VisitInstanceId == instanceId).Select(l => l.GuestMemberId).ToListAsync();
        var members = await db.VisitGuestMembers.AsNoTracking()
            .Where(m => links.Contains(m.GuestMemberId)).OrderBy(m => m.DisplayOrder).ToListAsync();

        var b = new Baseline
        {
            DelegationName = d.DelegationName,
            Purpose = d.Purpose ?? string.Empty,
            WorkingContent = d.WorkingContent,
            PlannedStartAt = instance.PlannedStartAt,
            PlannedEndAt = instance.PlannedEndAt,
            Visitors = members.Where(m => m.MemberType == "GUEST")
                .Select(m => new VisitorDto(m.FullName, m.Nationality ?? "", m.JobTitle ?? "", m.Organization ?? "")).ToList(),
        };
        mutate?.Invoke(b);

        return new VisitAmendmentProposalDto(
            instance.RowVersion, d.FormRevision, d.ApprovalRevision, "Cập nhật theo yêu cầu đoàn.",
            b.DelegationName, d.VisitType ?? "MEETING", d.VisitTypeOther, b.Purpose, b.WorkingContent,
            d.WorkingLanguage ?? "EN",
            new ContactPointDto(d.OperationalContactFullName ?? "", d.OperationalContactOrganization ?? "", "Trưởng phòng Hợp tác",
                d.OperationalContactPhone ?? "", d.OperationalContactEmail ?? ""),
            b.Visitors,
            new List<SupportTeamMemberDto>(),
            b.PlannedStartAt, b.PlannedEndAt);
    }

    private sealed class Baseline
    {
        public string DelegationName = "";
        public string Purpose = "";
        public string? WorkingContent;
        public DateTime PlannedStartAt;
        public DateTime PlannedEndAt;
        public List<VisitorDto> Visitors = new();
    }

    private static async Task CleanupAsync(ulong requestId)
    {
        if (requestId == 0) return;
        using var db = NewContext();
        var id = requestId;
        async Task Del(string sql) => await db.Database.ExecuteSqlRawAsync(sql, id);
        await Del("DELETE ac FROM visit_instance_amendment_changes ac JOIN visit_instance_amendments a ON a.amendment_id = ac.amendment_id WHERE a.visit_request_id = {0}");
        await Del("DELETE FROM visit_instance_amendments WHERE visit_request_id = {0}");
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

    // ── Tests ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An APPROVED campus a day away can still be amended. The 72-hour rule is about how far ahead a
    /// visit may be BOOKED, and asking it of a booking that already exists would close the amendment
    /// window on every visit inside three days — which is exactly when changes actually happen
    /// (repair prompt v2 §11.4, TC-72H-06/07).
    ///
    /// <para>
    /// The amendment answers to its OWN policy instead: <c>VisitMutationPolicy.RequiredLeadHours</c>,
    /// six hours, asserted here by the fact that a campus 24 hours out is comfortably inside it. The
    /// companion architecture test pins the same boundary structurally, so a future caller reaching for
    /// the registration floor from this flow fails there too.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_approved_campus_inside_72h_can_still_be_amended()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            // Well inside the 72h REGISTRATION floor, well outside the 6h ACTION cutoff.
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddHours(24));
            var proposal = await BaselineProposalAsync(instanceA, b => b.Purpose = "Đổi nội dung làm việc");

            using (var db = NewContext())
                await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);

            using (var db = NewContext())
            {
                var amendment = await db.VisitInstanceAmendments.AsNoTracking()
                    .SingleAsync(a => a.VisitInstanceId == instanceA);
                Assert.Equal(AmendmentStatuses.PendingApproval, amendment.Status);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Submit_stores_pending_proposal_without_touching_active_and_guards_duplicates()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var proposal = await BaselineProposalAsync(instanceA, b =>
            {
                b.DelegationName = "Đoàn Amend (đổi tên)";
                b.Purpose = "Mục đích mới";
                b.PlannedStartAt = b.PlannedStartAt.AddHours(2);
                b.PlannedEndAt = b.PlannedEndAt.AddHours(2);
            });

            VisitAmendmentDto dto;
            using (var db = NewContext())
                dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);

            Assert.Equal(AmendmentStatuses.PendingApproval, dto.Status);
            Assert.Contains(dto.Changes, c => c.FieldPath == VisitFieldClassifier.DelegationName
                                               && c.ChangeClass == AmendmentChangeClasses.ApprovalSensitive);
            Assert.Contains(dto.Changes, c => c.FieldPath == VisitFieldClassifier.PlannedStartAt
                                               && c.ChangeClass == AmendmentChangeClasses.Structural);

            using (var db = NewContext())
            {
                // ACTIVE snapshot untouched — a proposal is never active content.
                var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
                Assert.Equal("Đoàn Amend", detail.DelegationName);
                Assert.Equal(1u, detail.FormRevision);
                Assert.True(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.VisitRequestId == requestId && a.Action == "VISIT_AMENDMENT_SUBMITTED"));
            }

            // Second pending on the same instance → stable 409.
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentAlreadyPending, ex.ErrorCode);
            }
            // Base-revision conflict on the SIBLING (no pending there → the base check is reached) → 409.
            ulong instanceB;
            using (var db = NewContext())
                instanceB = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == requestId).OrderBy(c => c.CampusId)
                    .Select(c => c.VisitInstanceId).LastAsync();
            var staleOnB = (await BaselineProposalAsync(instanceB, b => b.Purpose = "Đổi trên B"))
                with { BaseFormRevision = 99 };
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceB, staleOnB), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentBaseRevisionConflict, ex.ErrorCode);
            }
            // Empty diff (identical proposal) on the sibling → rejected, and rejected AS an empty diff.
            // NOT_EDITABLE is the lifecycle/window refusal ("you may not propose here, now"); this
            // proposal is perfectly well-timed and simply says nothing, which is what the dedicated
            // NO_CHANGES code is for. The distinction is what lets the UI answer "nothing changed"
            // instead of telling the requester the campus is closed to changes.
            var identical = await BaselineProposalAsync(instanceB);
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceB, identical), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentNoChanges, ex.ErrorCode);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Deciding a proposal belongs to the campus's CURRENT HOST, and the seed's Host happens to be the
    /// Staff Leader who self-hosted — a real and common shape. The negative cases below are therefore
    /// chosen to be people who are NOT that Host: the leader of the sibling campus (a Staff Leader, on
    /// the same request, and still refused), an HO reader, and the requester.
    /// </summary>
    [Fact]
    public async Task Approve_by_current_host_applies_target_only_and_never_resets_approval()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, var instanceB) = await CreateApprovedAsync(Now.AddDays(20));
            var proposal = await BaselineProposalAsync(instanceA, b =>
            {
                b.DelegationName = "Đoàn Amend (đã duyệt đổi)";
                b.Visitors = new List<VisitorDto> { new("Guest B", "JP", "Manager", "OrgJP") }; // member replace
            });
            ulong amendmentId; ulong hostA; ulong leaderB; ulong campusA; ulong campusB;
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                var rowA = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                hostA = rowA.CurrentHostUserId!.Value;
                campusA = rowA.CampusId;
                var rowB = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceB)
                    .Select(c => new { c.CoordinatorUserId, c.CampusId }).SingleAsync();
                leaderB = rowB.CoordinatorUserId!.Value;
                campusB = rowB.CampusId;
            }

            // Everyone who is NOT this campus's current Host is refused — including a Staff Leader on
            // the very same request, which is the change: the role no longer carries the decision.
            using (var db = NewContext())
            {
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Decide(db, new FakeUser(leaderB, RoleCodes.Staff, UserSubRoles.Leader, campusB)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, null), CancellationToken.None));
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Decide(db, new FakeUser(leaderB, RoleCodes.Ho)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, null), CancellationToken.None));
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Decide(db, new FakeUser(Registrant)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, null), CancellationToken.None));
            }

            // The CURRENT Host of campus A — here, the leader who self-hosted it.
            using (var db = NewContext())
            {
                var res = await Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None);
                Assert.Equal(AmendmentStatuses.Approved, res.Status);
                Assert.Equal(2u, res.NewFormRevision);
                Assert.Equal(2u, res.NewApprovalRevision);
            }

            using (var db = NewContext())
            {
                var a = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
                var b = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceB);
                Assert.Equal("Đoàn Amend (đã duyệt đổi)", a.DelegationName); // applied on A
                Assert.Equal(2u, a.FormRevision);
                Assert.Equal("Đoàn Amend", b.DelegationName);                // sibling untouched
                Assert.Equal(1u, b.FormRevision);

                // Approval/instance status NEVER reset by an amendment.
                var instA = await db.VisitRequestCampuses.AsNoTracking().SingleAsync(c => c.VisitInstanceId == instanceA);
                Assert.Equal(VisitInstanceStatuses.Assigned, instA.Status);
                var visit = await db.VisitRequests.AsNoTracking().SingleAsync(v => v.VisitRequestId == requestId);
                Assert.Equal(VisitRequestStatuses.Approved, visit.Status);

                // Members replaced copy-on-write on A only.
                var linksA = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
                var membersA = await db.VisitGuestMembers.AsNoTracking()
                    .Where(m => linksA.Contains(m.GuestMemberId)).ToListAsync();
                Assert.Single(membersA);
                Assert.Equal("Guest B", membersA[0].FullName);
                var linksB = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceB).Select(l => l.GuestMemberId).ToListAsync();
                var membersB = await db.VisitGuestMembers.AsNoTracking()
                    .Where(m => linksB.Contains(m.GuestMemberId)).ToListAsync();
                Assert.Single(membersB);
                Assert.Equal("Guest A", membersB[0].FullName);

                // Revision history: the CREATE row (rev 1) + the post-apply AMENDMENT row (rev 2).
                var revisions = await db.VisitInstanceFormRevisionHistories.AsNoTracking()
                    .Where(r => r.VisitInstanceId == instanceA)
                    .OrderBy(r => r.FormRevision)
                    .Select(r => new { r.FormRevision, r.SourceType })
                    .ToListAsync();
                Assert.Equal(2, revisions.Count);
                Assert.Equal("CREATE", revisions[0].SourceType);
                Assert.Equal("AMENDMENT_APPLIED", revisions[1].SourceType);
                Assert.True(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(x => x.VisitRequestId == requestId && x.Action == "VISIT_AMENDMENT_APPROVED"));
                Assert.True(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(x => x.VisitRequestId == requestId && x.Action == "VISIT_INSTANCE_FORM_REVISION_APPLIED"));
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Amendment_member_change_is_copy_on_write_and_untouched_until_approved()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, var instanceB) = await CreateApprovedAsync(Now.AddDays(20));

            // A LEGACY-style shared guest member: ONE row linked from BOTH campuses (the case copy-on-write
            // exists for). Amending campus A must keep this row alive for the sibling.
            ulong sharedMemberId;
            using (var db = NewContext())
            {
                var shared = new VisitGuestMember
                {
                    VisitRequestId = requestId, FullName = "Shared Guest", Organization = "SharedOrg",
                    JobTitle = "Delegate", Nationality = "US", MemberType = "GUEST", DisplayOrder = 9,
                    CreatedAt = Now, CreatedBy = Registrant,
                };
                db.VisitGuestMembers.Add(shared);
                await db.SaveChangesAsync();
                sharedMemberId = shared.GuestMemberId;
                db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
                {
                    VisitRequestId = requestId, VisitInstanceId = instanceA,
                    GuestMemberId = sharedMemberId, DisplayOrder = 5, CreatedAt = Now, CreatedBy = Registrant,
                });
                db.VisitInstanceGuestMembers.Add(new VisitInstanceGuestMember
                {
                    VisitRequestId = requestId, VisitInstanceId = instanceB,
                    GuestMemberId = sharedMemberId, DisplayOrder = 5, CreatedAt = Now, CreatedBy = Registrant,
                });
                await db.SaveChangesAsync();
            }

            // Propose dropping the shared member from A (keep only the campus-own "Guest A").
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors = b.Visitors.Where(v => v.FullName == "Guest A").ToList());
            Assert.Single(proposal.Visitors); // sanity: shared member excluded from the proposal

            ulong amendmentId; ulong leaderA; ulong campusA;
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                Assert.Contains(dto.Changes, c => c.FieldPath == VisitFieldClassifier.Visitors
                                                   && c.ChangeClass == AmendmentChangeClasses.ApprovalSensitive);
                var row = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CoordinatorUserId, c.CampusId }).SingleAsync();
                leaderA = row.CoordinatorUserId!.Value;
                campusA = row.CampusId;
            }

            // BEFORE approve — the pending proposal must NOT touch the active member links on either campus.
            using (var db = NewContext())
            {
                var linksA = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
                Assert.Contains(sharedMemberId, linksA); // still linked from A
                Assert.Equal(2, linksA.Count);           // Guest A + Shared, unchanged
            }

            using (var db = NewContext())
            {
                var res = await Decide(db, new FakeUser(leaderA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None);
                Assert.Equal(AmendmentStatuses.Approved, res.Status);
            }

            using (var db = NewContext())
            {
                // Copy-on-write: A's link to the shared row is dropped, but the row LIVES because B still links it.
                var sharedStillExists = await db.VisitGuestMembers.AsNoTracking()
                    .AnyAsync(m => m.GuestMemberId == sharedMemberId);
                Assert.True(sharedStillExists);

                var linksA = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
                Assert.DoesNotContain(sharedMemberId, linksA);

                var linksB = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceB).Select(l => l.GuestMemberId).ToListAsync();
                Assert.Contains(sharedMemberId, linksB); // sibling keeps the shared member intact

                // A's applied member list is exactly the proposed single visitor.
                var membersA = await db.VisitGuestMembers.AsNoTracking()
                    .Where(m => linksA.Contains(m.GuestMemberId)).ToListAsync();
                Assert.Single(membersA);
                Assert.Equal("Guest A", membersA[0].FullName);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Reject_withdraw_and_expire_leave_the_active_snapshot_untouched()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, var instanceB) = await CreateApprovedAsync(Now.AddDays(20));
            ulong leaderA; ulong campusA;
            using (var db = NewContext())
            {
                var row = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CoordinatorUserId, c.CampusId }).SingleAsync();
                leaderA = row.CoordinatorUserId!.Value;
                campusA = row.CampusId;
            }

            // REJECT (reason required) keeps active content.
            var p1 = await BaselineProposalAsync(instanceA, b => b.Purpose = "Đổi mục đích 1");
            ulong am1;
            using (var db = NewContext())
                am1 = (await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, p1), CancellationToken.None)).AmendmentId;
            using (var db = NewContext())
            {
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Decide(db, new FakeUser(leaderA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                        new RejectVisitAmendmentCommand(instanceA, am1, ""), CancellationToken.None));
                var res = await Decide(db, new FakeUser(leaderA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new RejectVisitAmendmentCommand(instanceA, am1, "Không phù hợp."), CancellationToken.None);
                Assert.Equal(AmendmentStatuses.Rejected, res.Status);
            }

            // WITHDRAW (requester side).
            var p2 = await BaselineProposalAsync(instanceA, b => b.Purpose = "Đổi mục đích 2");
            ulong am2;
            using (var db = NewContext())
                am2 = (await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, p2), CancellationToken.None)).AmendmentId;
            using (var db = NewContext())
            {
                var res = await Decide(db, new FakeUser(Registrant)).Handle(
                    new WithdrawVisitAmendmentCommand(requestId, instanceA, am2), CancellationToken.None);
                Assert.Equal(AmendmentStatuses.Withdrawn, res.Status);
            }

            // EXPIRE: overdue pending amendment on the sibling → swept, idempotent.
            var p3 = await BaselineProposalAsync(instanceB, b => b.Purpose = "Đổi mục đích 3");
            ulong am3;
            using (var db = NewContext())
                am3 = (await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceB, p3), CancellationToken.None)).AmendmentId;
            using (var db = NewContext())
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE visit_instance_amendments SET expires_at = {0} WHERE amendment_id = {1}",
                    Now.AddHours(-1), am3);
            using (var db = NewContext())
            {
                var svc = new VisitAmendmentService(db, NullLogger<VisitAmendmentService>.Instance);
                Assert.True(await svc.ExpireDueAsync(Now, 50, CancellationToken.None) >= 1);
                Assert.Equal(0, await svc.ExpireDueAsync(Now, 50, CancellationToken.None)); // idempotent
            }

            using (var db = NewContext())
            {
                var am3Row = await db.VisitInstanceAmendments.AsNoTracking().SingleAsync(a => a.AmendmentId == am3);
                Assert.Equal(AmendmentStatuses.Expired, am3Row.Status);
                // Active content NEVER moved through any of the three outcomes.
                var a = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
                var b = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceB);
                Assert.Equal("Thăm", a.Purpose);
                Assert.Equal("Thăm", b.Purpose);
                Assert.Equal(1u, a.FormRevision);
                Assert.Equal(1u, b.FormRevision);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Pending_instance_and_late_window_cannot_amend()
    {
        RequireDb();
        ulong pendingRequest = 0, lateRequest = 0;
        try
        {
            // A still-WAITING instance routes to pending-edit, not amendments.
            using (var db = NewContext())
            {
                var handler = new CreateVisitRequestV2CommandHandler(
                    db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
                    new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingInvitationService(),
                    new UserProvisionService(db),
                    NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
                    new VisitRequestAggregateStatusService(db),
            new ProposedHostActivationService(db), new MySqlUserMutationLockService(db));
                var form = new VisitRequestFormDataV2(
                    "AM" + Guid.NewGuid().ToString("N"),
                    new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", V2SeedActor.Email(Registrant)),
                    null, new List<CampusVisitFormDto> { Campus("HN", Now.AddDays(20)) });
                pendingRequest = (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
            }
            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == pendingRequest).Select(c => c.VisitInstanceId).SingleAsync();
                var proposal = await BaselineProposalAsync(instance, b => b.Purpose = "Đổi");
                // ThrowsAny: the refusal is a VisitMutationRefusedException — a BusinessRuleException
                // that also names the campus and the deadline. The error CODE is what matters here.
                var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(pendingRequest, instance, proposal), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, ex.ErrorCode);
                // ...and it points at the right alternative rather than just saying no. That alternative
                // is now the PER-CAMPUS pending edit: the whole-request one is refused the moment any
                // sibling has been decided, so naming it would have sent people to a closed door.
                Assert.Contains("sửa thông tin cơ sở đang chờ duyệt", ex.Message);
            }

            // A decided instance starting inside the shared lead time → self-service window closed.
            // The refusal now carries the deadline and the start time, so the screen can say which
            // campus closed and when rather than only that something is not allowed.
            (lateRequest, var lateInstance, _) =
                await CreateApprovedAsync(Now.AddHours(VisitMutationPolicy.RequiredLeadHours - 1));
            using (var db = NewContext())
            {
                var proposal = await BaselineProposalAsync(lateInstance, b => b.Purpose = "Đổi gấp");
                var ex = await Assert.ThrowsAsync<VisitMutationRefusedException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(lateRequest, lateInstance, proposal), CancellationToken.None));
                Assert.Equal(VisitMutationErrorCodes.CutoffReached, ex.ErrorCode);
                Assert.Equal(VisitMutationPolicy.RequiredLeadHours, ex.RequiredLeadHours);
                Assert.NotNull(ex.CutoffAt);
                Assert.NotNull(ex.PlannedStartAt);
            }
        }
        finally
        {
            await CleanupAsync(pendingRequest);
            await CleanupAsync(lateRequest);
        }
    }

    /// <summary>
    /// Requester side AND current Host are the same person (§13/§14). There is nobody to wait for, so
    /// the proposal is decided in the same call — but it is still a proposal: the row exists, its change
    /// rows exist, requested_by and decided_by both name the actor, and the audit says self-approved.
    /// Making that person file, reload and approve their own proposal would be ceremony that teaches
    /// people to click through a review which reviews nothing.
    /// </summary>
    [Fact]
    public async Task A_requester_who_is_also_the_host_has_the_change_applied_in_the_same_call()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, var instanceB) = await CreateApprovedAsync(Now.AddDays(20));

            // Make campus A's HOST its operational contact, rather than making the registrant the Host.
            // The relation under test is the same — requester side AND current Host in one person — but
            // only this direction is a state the database will hold: current_host_user_id must be IC
            // Staff of the campus (or the approving leader), and the registrant here is a VISITOR, so
            // handing them the campus is refused by trg_visit_campuses_assignment_validate_bu. An
            // operational contact only has to be an ACTIVE user, so the Host can take that side.
            ulong selfActor;
            using (var db = NewContext())
            {
                var a = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceA);
                selfActor = a.CurrentHostUserId!.Value;
                a.OperationalContactUserId = selfActor;
                a.OperationalContactConfirmedAt = Now;
                a.OperationalContactConfirmationSource = OperationalContactSources.Transfer;
                a.RowVersion += 1;
                await db.SaveChangesAsync();
            }

            var proposal = await BaselineProposalAsync(instanceA, b => b.Purpose = "Tự cập nhật");
            ulong amendmentId;
            using (var db = NewContext())
            {
                var dto = await Submit(db, selfActor).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                Assert.Equal(AmendmentStatuses.Approved, dto.Status);
            }

            using (var db = NewContext())
            {
                // Applied — no pending proposal is left waiting for the person who wrote it.
                var detail = await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instanceA);
                Assert.Equal("Tự cập nhật", detail.Purpose);
                Assert.Equal(2u, detail.FormRevision);
                Assert.False(await db.VisitInstanceAmendments.AsNoTracking()
                    .AnyAsync(a => a.VisitInstanceId == instanceA && a.Status == AmendmentStatuses.PendingApproval));

                // History is kept in full (§13.1) — this is not a silent write to the active form.
                var amendment = await db.VisitInstanceAmendments.AsNoTracking()
                    .SingleAsync(a => a.AmendmentId == amendmentId);
                Assert.Equal(AmendmentStatuses.Approved, amendment.Status);
                Assert.Equal(selfActor, amendment.RequestedBy);
                Assert.Equal(selfActor, amendment.DecidedBy);
                Assert.True(await db.AuditLogs.AsNoTracking()
                    .AnyAsync(a => a.EntityId == amendmentId && a.Action == "VISIT_AMENDMENT_SELF_APPROVED"));

                // Sibling untouched, as ever.
                var b = await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instanceB);
                Assert.Equal(1u, b.FormRevision);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Authority travels with the Host role, and is read at DECISION time (§11/§69). A proposal filed
    /// while A held the campus is decided by whoever holds it when the decision is taken — so after a
    /// handover, A is refused and B decides. A is still a Staff Leader throughout, which is the point:
    /// the role never carried this.
    /// </summary>
    [Fact]
    public async Task A_host_handover_moves_the_decision_to_the_new_host_mid_proposal()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            ulong oldHost, campusA;
            using (var db = NewContext())
            {
                var row = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                oldHost = row.CurrentHostUserId!.Value;
                campusA = row.CampusId;
            }

            var proposal = await BaselineProposalAsync(instanceA, b => b.Purpose = "Đổi sau bàn giao");
            ulong amendmentId;
            using (var db = NewContext())
                amendmentId = (await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None)).AmendmentId;

            // The handover: campus A (HN) goes to an IC Staff of THAT campus. Not "any other valid user"
            // — trg_visit_campuses_assignment_validate_bu requires current_host_user_id to be IC Staff of
            // the campus or the approving Staff Leader, so a visitor account cannot hold a campus.
            const ulong newHost = 101;   // STAFF/STAFF, IC, campus 1 (HN) in the canonical seed
            using (var db = NewContext())
            {
                var a = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceA);
                a.CurrentHostUserId = newHost;
                a.HostAssignedBy = oldHost;
                a.HostAssignedAt = Now;
                a.RowVersion += 1;
                await db.SaveChangesAsync();
            }

            using (var db = NewContext())
            {
                // The previous Host — still the campus's Staff Leader — no longer decides.
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Decide(db, new FakeUser(oldHost, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, null), CancellationToken.None));
            }
            using (var db = NewContext())
            {
                var res = await Decide(db, new FakeUser(newHost)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None);
                Assert.Equal(AmendmentStatuses.Approved, res.Status);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// After approval the 72-hour registration floor does NOT apply to a proposed schedule (§39/§40).
    /// The campus's current date stays official until the Host approves, so a proposal is a request to
    /// move an agreed date rather than a new date filed for approval — and "could we shift it to
    /// tomorrow morning" has to be submittable at all.
    /// </summary>
    [Fact]
    public async Task A_proposed_schedule_after_approval_only_has_to_be_in_the_future()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));

            // The REFUSAL goes first, because a campus holds at most one proposal at a time: filing the
            // valid one first makes the second submit fail on "there is already a pending proposal",
            // which would pass an Assert.Throws while proving nothing about the schedule rule.
            //
            // A start that has already passed is the one thing a proposal cannot carry.
            var past = Now.AddHours(-1);
            var stale = await BaselineProposalAsync(instanceA, b =>
            {
                b.PlannedStartAt = past;
                b.PlannedEndAt = past.AddHours(2);
            });
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceA, stale), CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.InvalidVisitTime, ex.ErrorCode);
            }

            // Well inside the 72-hour floor, and comfortably in the future: accepted, because the floor
            // is a REGISTRATION rule and this campus is already approved.
            var soon = Now.AddHours(30);
            var proposal = await BaselineProposalAsync(instanceA, b =>
            {
                b.PlannedStartAt = soon;
                b.PlannedEndAt = soon.AddHours(2);
            });
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                Assert.Contains(dto.Changes, c => c.FieldPath == VisitFieldClassifier.PlannedStartAt);
            }
        }
        finally { await CleanupAsync(requestId); }
    }
}
