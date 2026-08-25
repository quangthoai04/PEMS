using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
using PEMS.Application.Partners.Common;
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
    // Mirrors VisitAmendmentService's own (private) serializer options — the approve path deserializes
    // change-row JSON with PropertyNamingPolicy = CamelCase and case-sensitive matching, so a change row
    // built by hand (bypassing SubmitAsync) must serialize with the same options or the round-trip
    // silently drops every property.
    private static readonly JsonSerializerOptions AmendmentJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
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
                .Select(m => new VisitorDto(m.FullName, m.Nationality ?? "", m.JobTitle ?? "", m.Organization ?? "",
                    m.OrganizationPartnerId)).ToList(),
            // Defaults to the address on record, which is what the real modal sends back untouched.
            ContactEmail = d.OperationalContactEmail ?? "",
            // Echoes the GENUINE DB value — including a real null — rather than coercing it to "".
            // A `?? ""` here used to mask FIX-A's asymmetric-normalization bug: it made every baseline
            // proposal's phone compare EQUAL to a null DB phone by accident (both sides read "" through
            // the old code path), so no test in this suite could ever have caught it. Tests that want the
            // masked behavior can still opt in via `mutate`.
            ContactPhone = d.OperationalContactPhone,
            // Echoes the CURRENT relationship unchanged, same reasoning as ContactPhone above: a
            // baseline proposal exists to change NOTHING unless `mutate` says otherwise, so it must
            // start from what is actually persisted (usually null in this fixture — the seeded
            // contact self-matches the REGISTRANT's own account, which is not a delegation member at
            // all) rather than a hardcoded default that would silently propose "no contact" against a
            // campus that already has one.
            ContactGuestMemberId = d.OperationalContactGuestMemberId,
        };
        mutate?.Invoke(b);

        return new VisitAmendmentProposalDto(
            instance.RowVersion, d.FormRevision, d.ApprovalRevision, "Cập nhật theo yêu cầu đoàn.",
            b.DelegationName, d.VisitType ?? "MEETING", d.VisitTypeOther, b.Purpose, b.WorkingContent,
            d.WorkingLanguage ?? "EN",
            new ContactPointDto(d.OperationalContactFullName ?? "", d.OperationalContactOrganization ?? "", "Trưởng phòng Hợp tác",
                b.ContactPhone, b.ContactEmail ?? ""),
            b.Visitors,
            new List<SupportTeamMemberDto>(),
            b.PlannedStartAt, b.PlannedEndAt,
            OperationalContactGuestMemberId: b.ContactGuestMemberId);
    }

    private sealed class Baseline
    {
        public string DelegationName = "";
        public string Purpose = "";
        public string? WorkingContent;
        public DateTime PlannedStartAt;
        public DateTime PlannedEndAt;
        public List<VisitorDto> Visitors = new();
        public string? ContactEmail;
        public string? ContactPhone;
        public ulong? ContactGuestMemberId;
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
            new ProposedHostActivationService(db, new MySqlUserMutationLockService(db)), new MySqlUserMutationLockService(db));
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

    /// <summary>
    /// WHO the operational contact is cannot be changed by a proposal.
    ///
    /// The address was classified APPROVAL_SENSITIVE, so an amendment could carry a new one and write it
    /// straight onto the campus when the Host approved — no invitation, no acceptance, no identity
    /// event. That is a second door onto the same identity, and the two doors disagreed: the contact
    /// workflow asks the new person to accept and leaves the old one in place until they do, while this
    /// one asked nobody. Describing the SAME person differently (name, phone) is still amendable, which
    /// is the distinction the refusal has to respect rather than banning the contact block outright.
    /// </summary>
    [Fact]
    public async Task A_proposal_cannot_change_the_contact_email_but_can_still_correct_the_rest()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(10));

            var handover = await BaselineProposalAsync(instanceA, b => b.ContactEmail = "nguoikhac@example.com");
            using (var db = NewContext())
            {
                var refused = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceA, handover), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.ContactEmailNotAmendable, refused.ErrorCode);
            }
            // Refused means nothing was written — not a proposal quietly filed minus the offending field.
            using (var db = NewContext())
                Assert.Empty(await db.VisitInstanceAmendments.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceA).ToListAsync());

            // The same address plus a genuine content change goes through, and stores no email row.
            var ordinary = await BaselineProposalAsync(instanceA, b => b.Purpose = "Đổi mục đích");
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, ordinary), CancellationToken.None);
                Assert.Contains(dto.Changes, c => c.FieldPath == VisitFieldClassifier.Purpose);
                Assert.DoesNotContain(dto.Changes, c => c.FieldPath == VisitFieldClassifier.OperationalContactEmail);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Member organizationPartnerId through submit+approve (PEMS_PATCH_SAFE_EDIT_AMENDMENT_PARTNER_SEARCH) ──

    [Fact]
    public async Task Member_organizationPartnerId_is_preserved_when_only_job_title_changes()
    {
        RequireDb();
        ulong requestId = 0;
        const ulong approvedPublicPartnerId = 103; // ACTIVE + APPROVED + PUBLIC (see RequestFormPartnerSelectableTests)
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            using (var db = NewContext())
            {
                var links = await db.VisitInstanceGuestMembers
                    .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
                var member = await db.VisitGuestMembers.SingleAsync(m => links.Contains(m.GuestMemberId));
                member.OrganizationPartnerId = approvedPublicPartnerId;
                await db.SaveChangesAsync();
            }

            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { JobTitle = "Trưởng đoàn" });

            ulong amendmentId; ulong hostA; ulong campusA;
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                var row = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                hostA = row.CurrentHostUserId!.Value;
                campusA = row.CampusId;
            }
            using (var db = NewContext())
                await Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None);

            using (var db = NewContext())
            {
                var links = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
                var member = await db.VisitGuestMembers.AsNoTracking().SingleAsync(m => links.Contains(m.GuestMemberId));
                Assert.Equal("Trưởng đoàn", member.JobTitle);
                // The relationship, not just the display text: this must be a real re-selection, not a
                // patch that quietly forgot the id that came with it.
                Assert.Equal((ulong?)approvedPublicPartnerId, member.OrganizationPartnerId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Changing_a_member_to_a_selectable_partner_persists_the_id_on_approve()
    {
        RequireDb();
        ulong requestId = 0;
        const ulong approvedPublicPartnerId = 103;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { Organization = "SeoulTech (được chọn)", OrganizationPartnerId = approvedPublicPartnerId });

            ulong amendmentId; ulong hostA; ulong campusA;
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                var row = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                hostA = row.CurrentHostUserId!.Value;
                campusA = row.CampusId;
            }
            using (var db = NewContext())
                await Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None);

            using (var db = NewContext())
            {
                var links = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
                var member = await db.VisitGuestMembers.AsNoTracking().SingleAsync(m => links.Contains(m.GuestMemberId));
                Assert.Equal((ulong?)approvedPublicPartnerId, member.OrganizationPartnerId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Submitting_a_non_selectable_member_partner_id_is_refused_at_submit()
    {
        RequireDb();
        ulong requestId = 0;
        const ulong pendingPartnerId = 120; // PENDING_APPROVAL, own-campus visible only
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { OrganizationPartnerId = pendingPartnerId });

            using var db = NewContext();
            var refusal = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(GuestOrganizationPartnerPolicy.NotSelectableCode, refusal.ErrorCode);
            Assert.False(await db.VisitInstanceAmendments.AsNoTracking().AnyAsync(a => a.VisitInstanceId == instanceA));
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Durable contact-member reference, NP-03 reused for amendment (P1 core) ───────────────────

    // Renamed while ALREADY linked: the relation must follow the SAME logical member by persisted
    // GuestMemberId + key, never by re-matching the (now-changed) name — operational-contact
    // consistency fix. Previously this test proved the ephemeral key alone was enough to ESTABLISH a
    // brand-new link from unlinked; that premise is gone (Amendment can no longer introduce a
    // relation), so this proves the surviving, narrower guarantee instead: PRESERVING an existing one
    // across a rename cannot be fooled into losing the link or into matching some other row by text.
    [Fact]
    public async Task Approving_a_rename_of_the_linked_member_preserves_the_relation_by_key_not_fingerprint()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
            }

            // Renaming the member IN THE SAME proposal is exactly what breaks a fingerprint match (the
            // old name is gone by the time linking runs) — this only succeeds if the key + persisted id,
            // not the text, is what resolves continuity.
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with
                {
                    FullName = "Guest A (đã đổi tên)", ClientMemberKey = "v1", GuestMemberId = guestA,
                });
            proposal = proposal with { OperationalContactClientMemberKey = "v1" };

            ulong amendmentId; ulong hostA; ulong campusA;
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                var row = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                hostA = row.CurrentHostUserId!.Value;
                campusA = row.CampusId;
            }
            using (var db = NewContext())
                await Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None);

            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.AsNoTracking().Include(c => c.FormDetail)
                    .SingleAsync(c => c.VisitInstanceId == instanceA);
                var linkedId = instance.FormDetail!.OperationalContactGuestMemberId;
                var member = await db.VisitGuestMembers.AsNoTracking().SingleAsync(m => m.GuestMemberId == linkedId);
                Assert.Equal("Guest A (đã đổi tên)", member.FullName);
                Assert.Equal(member.GuestMemberId, instance.FormDetail!.OperationalContactGuestMemberId);
                // The contact snapshot's own FullName synced to match — the "same logical member,
                // updated attributes" half of the fix.
                Assert.Equal("Guest A (đã đổi tên)", instance.FormDetail.OperationalContactFullName);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task No_contact_pick_does_not_fall_back_to_a_fingerprint_match_even_when_names_would_align()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            // The member is made to describe the SAME person as the contact snapshot — the exact shape
            // the legacy fingerprint fallback used to auto-link. Carrying a key but naming no pick must
            // still resolve to "outside the delegation", never a guess.
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { FullName = "Op Contact", JobTitle = "Trưởng phòng Hợp tác", Organization = "OpOrg" });
            proposal.Visitors[0] = proposal.Visitors[0] with { ClientMemberKey = "v1" };
            // OperationalContactClientMemberKey stays null/absent — no pick was made.

            ulong amendmentId; ulong hostA; ulong campusA;
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                var row = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                hostA = row.CurrentHostUserId!.Value;
                campusA = row.CampusId;
            }
            using (var db = NewContext())
                await Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None);

            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.AsNoTracking().Include(c => c.FormDetail)
                    .SingleAsync(c => c.VisitInstanceId == instanceA);
                Assert.Null(instance.FormDetail!.OperationalContactGuestMemberId);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task A_contact_key_that_names_no_member_in_the_proposal_is_refused_at_submit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var proposal = await BaselineProposalAsync(instanceA, b => b.Purpose = "Đổi mục đích");
            proposal = proposal with { OperationalContactClientMemberKey = "does-not-exist" };

            using var db = NewContext();
            var refusal = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, refusal.ErrorCode);
            Assert.False(await db.VisitInstanceAmendments.AsNoTracking().AnyAsync(a => a.VisitInstanceId == instanceA));
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Duplicate_member_keys_in_one_proposal_are_refused_at_submit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { ClientMemberKey = "dup" });
            proposal.ExternalSupportMembers.Add(new SupportTeamMemberDto(
                "Support B", "Trợ lý", "SupportOrg", "VN", null, "dup"));

            using var db = NewContext();
            var refusal = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, refusal.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Contact profile has exactly one door now — "Manage the contact role" (PEMS_CONTACT_ONE_DOOR) ──
    // The picker above (WHO the contact is) stays amendable; redescribing the contact itself does not.

    // AM-NEW-03: a handcrafted proposal that tries to redescribe the contact (name/organization/
    // job title/phone) — not just its email — is refused at submit, and nothing is written.
    [Fact]
    public async Task A_proposal_that_redescribes_the_contact_profile_is_refused_at_submit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(10));

            var redescribed = await BaselineProposalAsync(instanceA);
            redescribed = redescribed with
            {
                OperationalContact = redescribed.OperationalContact with { FullName = "Đầu mối khác tên" },
            };
            using (var db = NewContext())
            {
                var refused = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceA, redescribed), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.ContactProfileNotAmendable, refused.ErrorCode);
            }
            using (var db = NewContext())
                Assert.Empty(await db.VisitInstanceAmendments.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceA).ToListAsync());

            // Same guard for organization/job title/phone — one BuildChangeRows check covers all four.
            var reorganized = await BaselineProposalAsync(instanceA);
            reorganized = reorganized with
            {
                OperationalContact = reorganized.OperationalContact with { Phone = "+8498887777" },
            };
            using (var db = NewContext())
            {
                var refused = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceA, reorganized), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.ContactProfileNotAmendable, refused.ErrorCode);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // AM-NEW-01: an ordinary, unchanged-contact proposal never records a contact-profile change row —
    // the only door that may still write those four columns is "Manage the contact role".
    [Fact]
    public async Task An_ordinary_amendment_never_records_a_contact_profile_change_row()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(10));
            var proposal = await BaselineProposalAsync(instanceA, b => b.Purpose = "Đổi mục đích");

            using var db = NewContext();
            var dto = await Submit(db, Registrant).Handle(
                new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
            Assert.DoesNotContain(dto.Changes, c =>
                c.FieldPath == VisitFieldClassifier.OperationalContactFullName
                || c.FieldPath == VisitFieldClassifier.OperationalContactOrganization
                || c.FieldPath == VisitFieldClassifier.OperationalContactJobTitle
                || c.FieldPath == VisitFieldClassifier.OperationalContactPhone);
        }
        finally { await CleanupAsync(requestId); }
    }

    // FIX-A/B regression: a campus whose operational-contact phone is genuinely NULL on both the active
    // detail and the proposal must not be mistaken for a contact-profile change. Before the fix,
    // PhoneNumber.NormalizeOrOriginal(null) on the DB side ("") was compared against a null-preserving
    // ternary on the proposal side (null), so "" != null threw ContactProfileNotAmendable on ANY
    // amendment of a campus with no phone on file — even one that only touched Purpose.
    [Fact]
    public async Task Amendment_with_null_phone_on_both_sides_is_not_a_contact_profile_change()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(10));
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactPhone = null;
                await db.SaveChangesAsync();
            }

            // ContactPhone defaults to the genuine (now null) DB value — see BaselineProposalAsync.
            var proposal = await BaselineProposalAsync(instanceA, b => b.Purpose = "Đổi mục đích duy nhất");
            Assert.Null(proposal.OperationalContact.Phone);

            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                // Succeeded at all (no ContactProfileNotAmendable) — and, same as the ordinary case,
                // records no contact-profile change row: only Purpose actually moved.
                Assert.DoesNotContain(dto.Changes, c =>
                    c.FieldPath == VisitFieldClassifier.OperationalContactFullName
                    || c.FieldPath == VisitFieldClassifier.OperationalContactOrganization
                    || c.FieldPath == VisitFieldClassifier.OperationalContactJobTitle
                    || c.FieldPath == VisitFieldClassifier.OperationalContactPhone);
                Assert.Contains(dto.Changes, c => c.FieldPath == VisitFieldClassifier.Purpose);
            }

            // The active detail must be untouched until approval — a null-phone amendment is not special.
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instanceA);
                Assert.Null(detail.OperationalContactPhone);
                Assert.NotEqual("Đổi mục đích duy nhất", detail.Purpose);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // FIX-A guardrail: the null/blank symmetry fix must not weaken the rule for a REAL difference — a
    // campus with no phone on file, proposed a real one, is still a genuine profile change and must
    // still be refused (never silently accepted as "no value ~ no value").
    [Fact]
    public async Task Amendment_proposing_a_real_phone_over_a_null_one_still_refuses_at_submit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(10));
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactPhone = null;
                await db.SaveChangesAsync();
            }

            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactPhone = "+84987654321");

            using var db2 = NewContext();
            var refused = await Assert.ThrowsAnyAsync<BusinessRuleException>(() =>
                Submit(db2, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.ContactProfileNotAmendable, refused.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    // The durable contact-member link also PRESERVES onto an EXTERNAL_SUPPORT row, not just a GUEST
    // one — both kinds stay eligible for continuity (plan §16 / PEMS_CONTACT_ONE_DOOR item 6).
    // Previously this proved a support member could be picked as a BRAND NEW contact from unlinked;
    // that premise is gone, so this seeds the support member as the ALREADY-linked contact first (via
    // an approved amendment, matching how SeedTwoGuestMembersAsync seeds guests), then proves a
    // content-changing amendment that renames them preserves the link.
    [Fact]
    public async Task Approving_a_rename_preserves_the_relation_when_it_is_linked_to_a_support_member()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));

            // Seed: add the support member via its own approved amendment (member-list change only,
            // no relation touched — legitimate under the new contract).
            var seedProposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { ClientMemberKey = "v1" });
            seedProposal.ExternalSupportMembers.Add(new SupportTeamMemberDto(
                "Support Contact", "Trợ lý", "SupportOrg", "VN", null, "s1"));
            await SubmitAndApproveAsync(requestId, instanceA, seedProposal);

            ulong supportId;
            using (var db = NewContext())
            {
                var links = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
                supportId = (await db.VisitGuestMembers.AsNoTracking()
                    .SingleAsync(m => links.Contains(m.GuestMemberId) && m.MemberType == "EXTERNAL_SUPPORT")).GuestMemberId;
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = supportId;
                await db.SaveChangesAsync();
            }

            // Content-changing amendment: renames the support member, echoing back their OWN persisted
            // id as continuity evidence. BaselineProposalAsync never echoes back EXISTING support rows
            // (unlike Visitors, which does query real GUEST members) — it always starts from an empty
            // ExternalSupportMembers list — so the renamed row is added explicitly here rather than
            // fetched off the baseline.
            var proposal = await BaselineProposalAsync(instanceA, b => { });
            proposal.ExternalSupportMembers.Add(new SupportTeamMemberDto(
                "Support Contact (đổi tên)", "Trợ lý", "SupportOrg", "VN", null, "s1", supportId));
            proposal.Visitors[0] = proposal.Visitors[0] with { ClientMemberKey = "v1" };
            proposal = proposal with { OperationalContactClientMemberKey = "s1" };

            ulong amendmentId; ulong hostA; ulong campusA;
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                var row = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                hostA = row.CurrentHostUserId!.Value;
                campusA = row.CampusId;
            }
            using (var db = NewContext())
                await Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None);

            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.AsNoTracking().Include(c => c.FormDetail)
                    .SingleAsync(c => c.VisitInstanceId == instanceA);
                var links = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
                var support = await db.VisitGuestMembers.AsNoTracking()
                    .SingleAsync(m => links.Contains(m.GuestMemberId) && m.MemberType == "EXTERNAL_SUPPORT");
                Assert.Equal("Support Contact (đổi tên)", support.FullName);
                Assert.Equal(support.GuestMemberId, instance.FormDetail!.OperationalContactGuestMemberId);
                Assert.Equal("Support Contact (đổi tên)", instance.FormDetail.OperationalContactFullName);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Patch 4 — nationality contract ──────────────────────────────────────────

    /// <summary>
    /// A member-list amendment is, from the write path's point of view, a copy-on-write replace exactly
    /// like create — StageReplaceMembers resolves-or-rejects every row. Structural submit validation
    /// (NotEmpty + MaximumLength) still passes an unresolvable nationality — same as before Patch 4 —
    /// because those FluentValidation rules are also reused for pending-edit, where they must not
    /// reject legacy content; the rejection happens at APPROVAL, the moment the new member rows are
    /// actually written.
    /// </summary>
    [Fact]
    public async Task Approving_a_member_list_amendment_rejects_an_unresolvable_nationality()
    {
        // Patch 4 hardening H4-4: a GENUINELY new/changed member nationality that does not resolve is
        // now refused at SUBMIT (see the next test) — this test proves the approval-time guard still
        // exists too (defense in depth), by writing a PENDING amendment directly rather than through
        // SubmitAsync, simulating one somehow filed before the submit-time guard existed.
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { Nationality = "abcxyzcountry" });

            ulong amendmentId; ulong hostA; ulong campusA;
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                var amendment = new VisitInstanceAmendment
                {
                    VisitRequestId = requestId,
                    VisitInstanceId = instanceA,
                    AmendmentNo = 1,
                    Status = AmendmentStatuses.PendingApproval,
                    BaseFormRevision = detail.FormRevision,
                    BaseApprovalRevision = detail.ApprovalRevision,
                    RequestedBy = Registrant,
                    RequestedAt = Now,
                    ExpiresAt = proposal.PlannedStartAt.AddHours(-VisitMutationPolicy.MutationCutoffHours),
                    ExpectedInstanceRowVersion = (uint)proposal.ExpectedInstanceRowVersion,
                    CreatedAt = Now,
                };
                amendment.Changes.Add(new VisitInstanceAmendmentChange
                {
                    FieldPath = VisitFieldClassifier.Visitors,
                    ChangeClass = AmendmentChangeClasses.ApprovalSensitive,
                    OldValueJson = null,
                    NewValueJson = JsonSerializer.Serialize(proposal.Visitors, AmendmentJson),
                    IsSensitive = true,
                    DisplayOrder = 0,
                    CreatedAt = Now,
                });
                db.VisitInstanceAmendments.Add(amendment);
                await db.SaveChangesAsync();
                amendmentId = amendment.AmendmentId;
                var row = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                hostA = row.CurrentHostUserId!.Value;
                campusA = row.CampusId;
            }
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None));
                Assert.Equal(VisitRequestErrorCodes.InvalidNationality, ex.ErrorCode);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// Patch 4 hardening H4-4: the normal path a real proposal takes. A member's nationality is
    /// GENUINELY new-or-changed content (the rest of that row is untouched, only nationality moved to
    /// garbage) and must now be refused at SUBMIT — before a PENDING amendment nobody could ever
    /// approve is created, notifying a Requester and a Staff Leader about nothing.
    /// </summary>
    [Fact]
    public async Task Submitting_a_member_list_amendment_rejects_an_unresolvable_nationality_before_creating_it()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { Nationality = "abcxyzcountry" });

            using var db = NewContext();
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitRequestErrorCodes.InvalidNationality, ex.ErrorCode);

            Assert.False(await db.VisitInstanceAmendments.AnyAsync(a => a.VisitInstanceId == instanceA),
                "a proposal that could never be approved must not be created as a PENDING amendment");
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// The other half of H4-4/H4-3: a member row whose content is BYTE-IDENTICAL to what is already on
    /// the campus (here, an unresolvable legacy nationality nobody touched) must not block a proposal
    /// that only changes something else about the same campus.
    /// </summary>
    [Fact]
    public async Task Submitting_an_amendment_is_not_blocked_by_an_untouched_unresolvable_legacy_member_nationality()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            using (var db = NewContext())
            {
                var links = await db.VisitInstanceGuestMembers.Where(l => l.VisitInstanceId == instanceA).ToListAsync();
                var member = await db.VisitGuestMembers.SingleAsync(m => m.GuestMemberId == links[0].GuestMemberId);
                member.Nationality = "Legacy Unrecognized Value";
                await db.SaveChangesAsync();
            }
            var proposal = await BaselineProposalAsync(instanceA, b => b.Purpose = "Mục đích sửa qua đề xuất");
            Assert.Equal("Legacy Unrecognized Value", proposal.Visitors[0].Nationality); // echoed back untouched

            using var db2 = NewContext();
            var dto = await Submit(db2, Registrant).Handle(
                new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
            Assert.True(dto.AmendmentId > 0);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── Relationship-only amendments (plan CanhIter3FixBug "Đầu mối hiện tại có nằm trong danh sách
    //    đoàn không?") — member list UNCHANGED, only WHO the contact is (by persistent GuestMemberId)
    //    moves. §20 CASE 1–13. ──────────────────────────────────────────────────────────────────────

    /// <summary>Approves a member-replacement amendment that gives ONE campus two named guests, then
    /// returns their real DB ids in submission order — the fixture every relationship-only test below
    /// builds on. Uses the EXISTING, already-proven ClientMemberKey/member-replace mechanism (unchanged
    /// by this fix) purely as setup.</summary>
    private async Task<(ulong GuestAId, ulong GuestBId)> SeedTwoGuestMembersAsync(ulong requestId, ulong instanceId)
    {
        var proposal = await BaselineProposalAsync(instanceId, b =>
            b.Visitors = new List<VisitorDto>
            {
                new("Guest A", "VN", "Guest", "GuestOrg", ClientMemberKey: "seed-a"),
                new("Guest B", "JP", "Manager", "OrgJP", ClientMemberKey: "seed-b"),
            });
        ulong amendmentId, hostId, campusId;
        using (var db = NewContext())
        {
            var dto = await Submit(db, Registrant).Handle(
                new SubmitVisitAmendmentCommand(requestId, instanceId, proposal), CancellationToken.None);
            amendmentId = dto.AmendmentId;
            var row = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instanceId)
                .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
            hostId = row.CurrentHostUserId!.Value;
            campusId = row.CampusId;
        }
        using (var db = NewContext())
            await Decide(db, new FakeUser(hostId, RoleCodes.Staff, UserSubRoles.Leader, campusId)).Handle(
                new ApproveVisitAmendmentCommand(instanceId, amendmentId, "OK"), CancellationToken.None);

        using var db2 = NewContext();
        var links = await db2.VisitInstanceGuestMembers.AsNoTracking()
            .Where(l => l.VisitInstanceId == instanceId).Select(l => l.GuestMemberId).ToListAsync();
        var members = await db2.VisitGuestMembers.AsNoTracking()
            .Where(m => links.Contains(m.GuestMemberId)).ToListAsync();
        return (members.Single(m => m.FullName == "Guest A").GuestMemberId,
                members.Single(m => m.FullName == "Guest B").GuestMemberId);
    }

    /// <summary>Submits, then approves (as the campus's current Host), a relationship-only proposal.
    /// Returns the FormRevision/ApprovalRevision BEFORE the amendment, so callers can assert "+1
    /// exactly" without hardcoding a number that would drift as fixtures change.</summary>
    private async Task<(uint FormRevisionBefore, uint ApprovalRevisionBefore)> SubmitAndApproveAsync(
        ulong requestId, ulong instanceId, VisitAmendmentProposalDto proposal)
    {
        uint formBefore, approvalBefore;
        using (var db0 = NewContext())
        {
            var before = await db0.VisitInstanceFormDetails.AsNoTracking()
                .SingleAsync(d => d.VisitInstanceId == instanceId);
            formBefore = before.FormRevision;
            approvalBefore = before.ApprovalRevision;
        }

        ulong amendmentId, hostId, campusId;
        using (var db = NewContext())
        {
            var dto = await Submit(db, Registrant).Handle(
                new SubmitVisitAmendmentCommand(requestId, instanceId, proposal), CancellationToken.None);
            amendmentId = dto.AmendmentId;
            var row = await db.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instanceId)
                .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
            hostId = row.CurrentHostUserId!.Value;
            campusId = row.CampusId;
        }
        using (var db = NewContext())
            await Decide(db, new FakeUser(hostId, RoleCodes.Staff, UserSubRoles.Leader, campusId)).Handle(
                new ApproveVisitAmendmentCommand(instanceId, amendmentId, "OK"), CancellationToken.None);
        return (formBefore, approvalBefore);
    }

    // AMD-REL-01 (operational-contact consistency fix): introducing a relation from unlinked, member
    // list echoed back UNCHANGED, is REJECTED at submit — Amendment may never establish who the
    // contact is; that is exclusively Safe Edit's (link) or Replace/Transfer's job.
    [Fact]
    public async Task AmdRel_introducing_a_relation_when_currently_unlinked_is_rejected_at_submit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(20));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
                Assert.Null((await db.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instanceA)).OperationalContactGuestMemberId);

            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactGuestMemberId = guestA);
            Assert.Equal(2, proposal.Visitors.Count); // member list echoed back UNCHANGED

            using var db2 = NewContext();
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db2, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, refused.ErrorCode);

            // Zero mutation: no PENDING amendment row from THIS refused submit (SeedTwoGuestMembersAsync
            // leaves its own already-APPROVED seed amendment behind, which is not what this asserts).
            Assert.False(await db2.VisitInstanceAmendments.AsNoTracking()
                .AnyAsync(a => a.VisitInstanceId == instanceA && a.Status == AmendmentStatuses.PendingApproval));
            var detail = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Null(detail.OperationalContactGuestMemberId);
            var linkedIds = await db2.VisitInstanceGuestMembers.AsNoTracking()
                .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
            Assert.Equal(2, linkedIds.Count);
        }
        finally { await CleanupAsync(requestId); }
    }

    // AMD-REL-02: an active LINKED relation, proposal tries to remove it (member list unchanged) — rejected.
    [Fact]
    public async Task AmdRel_removing_an_existing_relation_via_a_member_list_unchanged_proposal_is_rejected()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(21));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
            }

            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactGuestMemberId = null);

            using var db2 = NewContext();
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db2, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, refused.ErrorCode);

            var after = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Equal(guestA, after.OperationalContactGuestMemberId);
            Assert.Equal("Guest A", (await db2.VisitGuestMembers.AsNoTracking().SingleAsync(m => m.GuestMemberId == guestA)).FullName);
        }
        finally { await CleanupAsync(requestId); }
    }

    // AMD-REL-03: active LINKED to Guest A, proposal tries to switch to Guest B (member list unchanged).
    [Fact]
    public async Task AmdRel_switching_between_two_existing_members_without_a_content_change_is_rejected()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(22));
            var (guestA, guestB) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
            }

            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactGuestMemberId = guestB);

            using var db2 = NewContext();
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db2, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, refused.ErrorCode);

            var after = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Equal(guestA, after.OperationalContactGuestMemberId);
            // Both members keep their OWN original ids — nobody was deleted or re-inserted.
            var members = await db2.VisitGuestMembers.AsNoTracking()
                .Where(m => m.GuestMemberId == guestA || m.GuestMemberId == guestB).ToListAsync();
            Assert.Equal(2, members.Count);
            Assert.Contains(members, m => m.GuestMemberId == guestA && m.FullName == "Guest A");
            Assert.Contains(members, m => m.GuestMemberId == guestB && m.FullName == "Guest B");
        }
        finally { await CleanupAsync(requestId); }
    }

    // CASE 4: Guest A → Guest A, reason changed only → AmendmentNoChanges.
    [Fact]
    public async Task RelationshipOnly_Case4_Unchanged_relationship_with_only_a_new_reason_is_refused_as_no_changes()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(23));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
            }

            // Same relation, same everything else — only the free-text reason differs. Reason alone is
            // NEVER a business change (BuildChangeRows never diffs it into a change row at all).
            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactGuestMemberId = guestA);
            proposal = proposal with { Reason = "Một lý do bất kỳ, không đổi gì khác" };

            using var db2 = NewContext();
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db2, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNoChanges, refused.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    // A sibling campus's member id is still rejected — no longer as a "not found" identity refusal,
    // but as an attempted relation-state change, exactly like any other proposed relation difference.
    [Fact]
    public async Task AmdRel_a_sibling_campuss_member_id_is_still_rejected_at_submit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, var instanceB) = await CreateApprovedAsync(Now.AddDays(24));
            // instanceB's own seeded guest — never a member of instanceA.
            List<ulong> linksB;
            using (var db = NewContext())
                linksB = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceB).Select(l => l.GuestMemberId).ToListAsync();
            var siblingMemberId = linksB.Single();

            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactGuestMemberId = siblingMemberId);

            using var db2 = NewContext();
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db2, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, refused.ErrorCode);

            // Refused cleanly — no amendment row, no relationship change, no side effects.
            Assert.False(await db2.VisitInstanceAmendments.AsNoTracking().AnyAsync(a => a.VisitInstanceId == instanceA));
            var detailA = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Null(detailA.OperationalContactGuestMemberId);
        }
        finally { await CleanupAsync(requestId); }
    }

    // A nonexistent GuestMemberId is still rejected — same "you may not touch the relation" refusal
    // as any other proposed difference, member-list unchanged.
    [Fact]
    public async Task AmdRel_a_nonexistent_member_id_is_still_rejected_at_submit()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(25));
            const ulong bogusId = 999_999_999UL;
            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactGuestMemberId = bogusId);

            using var db = NewContext();
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, refused.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    // A brand-new member (added by this same proposal) picked as contact from an UNLINKED campus is
    // RelationIntroduced — rejected, even though the member-list change itself would otherwise be a
    // perfectly legitimate amendment (operational-contact consistency fix: the old "ephemeral key
    // flow still handles it" behavior this test used to prove no longer exists — establishing a NEW
    // relation is Safe Edit's/Transfer's job now, regardless of whether it rides on a member-list
    // change or not).
    [Fact]
    public async Task AmdRel_a_brand_new_member_picked_as_contact_from_unlinked_is_rejected()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(26));
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors = new List<VisitorDto>(b.Visitors) { new("Guest C", "KR", "Director", "OrgKR", ClientMemberKey: "c-key") });
            proposal = proposal with { OperationalContactClientMemberKey = "c-key" };

            using var db = NewContext();
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, refused.ErrorCode);
            Assert.False(await db.VisitInstanceAmendments.AsNoTracking().AnyAsync(a => a.VisitInstanceId == instanceA));
        }
        finally { await CleanupAsync(requestId); }
    }

    // A rejected relation-change attempt leaves member ids, OrganizationPartnerId and partner links
    // completely untouched — zero mutation covers everything, not just the relation field.
    [Fact]
    public async Task AmdRel_a_rejected_relation_attempt_preserves_member_ids_partner_id_and_partner_link_row()
    {
        RequireDb();
        ulong requestId = 0;
        const ulong approvedPublicPartnerId = 103; // ACTIVE + APPROVED + PUBLIC (see RequestFormPartnerSelectableTests)
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(27));
            var (guestA, guestB) = await SeedTwoGuestMembersAsync(requestId, instanceA);

            ulong linkId;
            using (var db = NewContext())
            {
                var member = await db.VisitGuestMembers.SingleAsync(m => m.GuestMemberId == guestA);
                member.OrganizationPartnerId = approvedPublicPartnerId;
                await db.SaveChangesAsync();

                db.VisitGuestPartnerLinks.Add(new PEMS.Domain.Entities.Partners.VisitGuestPartnerLink
                {
                    VisitRequestId = requestId,
                    VisitInstanceId = instanceA,
                    GuestMemberId = guestA,
                    PartnerId = approvedPublicPartnerId,
                    MatchSource = PEMS.Application.Partners.Common.PartnerLinkMatchSources.Manual,
                    MatchStatus = PEMS.Application.Partners.Common.PartnerLinkMatchStatuses.Confirmed,
                    CreatedAt = Now,
                    CreatedBy = Registrant,
                });
                await db.SaveChangesAsync();
                linkId = (await db.VisitGuestPartnerLinks.SingleAsync(l => l.GuestMemberId == guestA)).LinkId;
            }

            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactGuestMemberId = guestB);
            using var db2 = NewContext();
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db2, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));

            var memberAfter = await db2.VisitGuestMembers.AsNoTracking().SingleAsync(m => m.GuestMemberId == guestA);
            Assert.Equal(approvedPublicPartnerId, memberAfter.OrganizationPartnerId);
            var linkAfter = await db2.VisitGuestPartnerLinks.AsNoTracking().SingleAsync(l => l.GuestMemberId == guestA);
            Assert.Equal(linkId, linkAfter.LinkId); // same row — never orphaned/reseeded
            var linkedIds = await db2.VisitInstanceGuestMembers.AsNoTracking()
                .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
            Assert.Equal(new[] { guestA, guestB }.OrderBy(x => x), linkedIds.OrderBy(x => x));
        }
        finally { await CleanupAsync(requestId); }
    }

    // AMD-REL-06 (operational-contact consistency fix): the one thing this whole fix EXISTS to make
    // work — editing the LINKED member's own shared fields through a content-changing amendment DOES
    // sync FullName/JobTitle/Organization onto the contact once approved (previously this exact
    // scenario was asserted to NEVER happen; that assertion is now flipped). A profile rewrite bundled
    // in the SAME proposal (never a member edit, a hand-typed contact field) remains refused outright —
    // the two mechanisms stay separate.
    [Fact]
    public async Task AmdRel_approving_a_content_change_to_the_linked_member_syncs_the_contact_profile()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(28));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            uint formRevBefore, approvalRevBefore;
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
                formRevBefore = detail.FormRevision;
                approvalRevBefore = detail.ApprovalRevision;
            }

            // Content-changing proposal: Guest A's OWN JobTitle changes, echoed back with a fresh
            // ClientMemberKey + Guest A's OWN real persisted GuestMemberId as continuity evidence.
            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with
                {
                    JobTitle = "Senior Director", ClientMemberKey = "a-key", GuestMemberId = guestA,
                });
            proposal = proposal with { OperationalContactClientMemberKey = "a-key" };

            var (dto, hostA, campusA) = (default(VisitAmendmentDto), 0UL, 0UL);
            using (var db = NewContext())
            {
                dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                var row = await db.VisitRequestCampuses.AsNoTracking().Where(c => c.VisitInstanceId == instanceA)
                    .Select(c => new { c.CurrentHostUserId, c.CampusId }).SingleAsync();
                hostA = row.CurrentHostUserId!.Value;
                campusA = row.CampusId;
            }
            using (var db = NewContext())
                await Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                    new ApproveVisitAmendmentCommand(instanceA, dto!.AmendmentId, "OK"), CancellationToken.None);

            using var db2 = NewContext();
            var detailAfter = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            var linkedIds = await db2.VisitInstanceGuestMembers.AsNoTracking()
                .Where(l => l.VisitInstanceId == instanceA).Select(l => l.GuestMemberId).ToListAsync();
            var newA = await db2.VisitGuestMembers.AsNoTracking()
                .SingleAsync(m => linkedIds.Contains(m.GuestMemberId) && m.FullName == "Guest A");
            Assert.Equal(newA.GuestMemberId, detailAfter.OperationalContactGuestMemberId);
            Assert.Equal("Senior Director", detailAfter.OperationalContactJobTitle);
            Assert.True(await db2.AuditLogs.AsNoTracking().AnyAsync(a =>
                a.VisitInstanceId == instanceA
                && a.Changes.Any(c => c.FieldName == "operational_contact_job_title" && c.NewValueText == "Senior Director")));
            // Section 20: exactly ONE revision bump for the whole approve, even though it also does
            // member COW + relation re-link + member→contact shared-field sync — the sync is not a
            // second approved-revision transition.
            Assert.Equal(formRevBefore + 1, detailAfter.FormRevision);
            Assert.Equal(approvalRevBefore + 1, detailAfter.ApprovalRevision);

            // A hand-typed profile rewrite bundled with a relationship-preserving pick remains refused —
            // the sync above happens ONLY as a consequence of editing the member, never by typing over
            // the contact block directly.
            var withProfileRewrite = await BaselineProposalAsync(instanceA, b => { });
            withProfileRewrite = withProfileRewrite with
            {
                OperationalContact = withProfileRewrite.OperationalContact with { FullName = "Someone Else" },
            };
            var refused = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Submit(db2, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, withProfileRewrite), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.ContactProfileNotAmendable, refused.ErrorCode);
        }
        finally { await CleanupAsync(requestId); }
    }

    // AMD-REL-04b: Submit merely PROPOSES — the active/live campus data must be untouched until Approve,
    // even for the exact continuity-preserving content change the section above proves Approve applies.
    [Fact]
    public async Task AmdRel_submitting_a_linked_members_content_change_never_mutates_active_state()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(29));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
            }

            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with
                {
                    JobTitle = "Senior Director", ClientMemberKey = "a-key", GuestMemberId = guestA,
                });
            proposal = proposal with { OperationalContactClientMemberKey = "a-key" };

            using var db2 = NewContext();
            await Submit(db2, Registrant).Handle(
                new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);

            // Submit alone must not have touched the active detail/member at all — Approve is what applies.
            var activeDetail = await db2.VisitInstanceFormDetails.AsNoTracking()
                .SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Equal(guestA, activeDetail.OperationalContactGuestMemberId);
            Assert.NotEqual("Senior Director", activeDetail.OperationalContactJobTitle);
            var activeMember = await db2.VisitGuestMembers.AsNoTracking()
                .SingleAsync(m => m.GuestMemberId == guestA);
            Assert.Equal("Guest", activeMember.JobTitle);
        }
        finally { await CleanupAsync(requestId); }
    }

    // AMD-REL-05: a member list that genuinely changes while staying unlinked (no key proposed at all)
    // is a perfectly ordinary amendment — the continuity check must never block content that has nothing
    // to do with the (nonexistent) relation.
    [Fact]
    public async Task AmdRel_a_genuine_member_edit_that_stays_unlinked_is_a_valid_proposal()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(27));
            // Sanity: this campus starts genuinely unlinked (Campus()'s contact self-matches the
            // registrant, never a delegation member).
            using (var db0 = NewContext())
                Assert.Null((await db0.VisitInstanceFormDetails.AsNoTracking()
                    .SingleAsync(d => d.VisitInstanceId == instanceA)).OperationalContactGuestMemberId);

            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { Organization = "Tổ chức mới" });
            // OperationalContactClientMemberKey stays null/absent — no pick was made.

            using var db = NewContext();
            var dto = await Submit(db, Registrant).Handle(
                new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);

            Assert.True(await db.VisitInstanceAmendments.AsNoTracking()
                .AnyAsync(a => a.AmendmentId == dto.AmendmentId && a.Status == AmendmentStatuses.PendingApproval));
            var stillUnlinked = await db.VisitInstanceFormDetails.AsNoTracking()
                .SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Null(stillUnlinked.OperationalContactGuestMemberId); // proposal only — Approve not called
        }
        finally { await CleanupAsync(requestId); }
    }

    // AMD-COW-05: self-approved COW-preserving content change (requester is also the campus's current
    // Host) still applies in the same call.
    [Fact]
    public async Task AmdRel_self_approved_path_applies_a_COW_preserving_content_change_in_the_same_call()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(29));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
            }

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

            var proposal = await BaselineProposalAsync(instanceA, b =>
                b.Visitors[0] = b.Visitors[0] with { JobTitle = "Director", ClientMemberKey = "a-key", GuestMemberId = guestA });
            proposal = proposal with { OperationalContactClientMemberKey = "a-key" };
            using (var db = NewContext())
            {
                var dto = await Submit(db, selfActor).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                Assert.Equal(AmendmentStatuses.Approved, dto.Status); // applied in the same call
            }

            using var db2 = NewContext();
            var detailAfter = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Equal("Director", detailAfter.OperationalContactJobTitle);
            Assert.NotNull(detailAfter.OperationalContactGuestMemberId);
            Assert.False(await db2.VisitInstanceAmendments.AsNoTracking()
                .AnyAsync(a => a.VisitInstanceId == instanceA && a.Status == AmendmentStatuses.PendingApproval));
        }
        finally { await CleanupAsync(requestId); }
    }

    // AMD-REL-04: a proposal built from a STALE snapshot is refused, exactly like any other amendment
    // — the base-revision/row-version guards run before the relation continuity check is even reached.
    // Uses two successive CONTENT-changing proposals (Purpose) rather than relation picks, since a
    // relation-only proposal is no longer a legitimate amendment at all.
    [Fact]
    public async Task AmdRel_a_stale_base_revision_is_refused_as_a_conflict()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(30));

            var stale = await BaselineProposalAsync(instanceA, b => b.Purpose = "Mục đích cũ (thua)");
            // Move the campus on (a different, legitimate content-changing amendment, approved) so
            // `stale`'s base revision/version now describe a state that no longer exists.
            var advance = await BaselineProposalAsync(instanceA, b => b.Purpose = "Mục đích mới (thắng)");
            await SubmitAndApproveAsync(requestId, instanceA, advance);

            using var db = NewContext();
            var refused = await Assert.ThrowsAsync<ConflictException>(() =>
                Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, stale), CancellationToken.None));
            Assert.Equal(VisitFormV2ErrorCodes.VisitFormConcurrencyConflict, refused.ErrorCode);

            // The winner's content stands — the stale loser never touched anything.
            var detail = await db.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Equal("Mục đích mới (thắng)", detail.Purpose);
        }
        finally { await CleanupAsync(requestId); }
    }

    // A rejected relation-change attempt on ONE campus leaves an untouched sibling byte-for-byte the
    // same — status, host, decision, members, and its own relationship.
    [Fact]
    public async Task AmdRel_a_rejected_relation_attempt_leaves_a_sibling_campus_completely_unaffected()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, var instanceB) = await CreateApprovedAsync(Now.AddDays(31));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);

            ulong siblingContactBefore;
            using (var db = NewContext())
            {
                var detailB = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceB);
                var linkB = await db.VisitInstanceGuestMembers.AsNoTracking()
                    .Where(l => l.VisitInstanceId == instanceB).Select(l => l.GuestMemberId).SingleAsync();
                detailB.OperationalContactGuestMemberId = linkB;
                await db.SaveChangesAsync();
                siblingContactBefore = linkB;
            }
            using var dbSnap = NewContext();
            var siblingBefore = await dbSnap.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instanceB)
                .Select(c => new { c.Status, c.CurrentHostUserId, c.DecidedBy, c.RowVersion }).SingleAsync();
            var siblingFormRevBefore = (await dbSnap.VisitInstanceFormDetails.AsNoTracking()
                .SingleAsync(d => d.VisitInstanceId == instanceB)).FormRevision;
            var siblingRevisionCountBefore = await dbSnap.VisitInstanceFormRevisionHistories.AsNoTracking()
                .CountAsync(r => r.VisitInstanceId == instanceB);

            var proposal = await BaselineProposalAsync(instanceA, b => b.ContactGuestMemberId = guestA);
            using (var db2 = NewContext())
                await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Submit(db2, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None));

            using var db3 = NewContext();
            var siblingAfter = await db3.VisitRequestCampuses.AsNoTracking()
                .Where(c => c.VisitInstanceId == instanceB)
                .Select(c => new { c.Status, c.CurrentHostUserId, c.DecidedBy, c.RowVersion }).SingleAsync();
            Assert.Equal(siblingBefore, siblingAfter);
            var detailBAfter = await db3.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceB);
            Assert.Equal(siblingFormRevBefore, detailBAfter.FormRevision);
            Assert.Equal(siblingContactBefore, detailBAfter.OperationalContactGuestMemberId);
            var siblingRevisionCountAfter = await db3.VisitInstanceFormRevisionHistories.AsNoTracking()
                .CountAsync(r => r.VisitInstanceId == instanceB);
            Assert.Equal(siblingRevisionCountBefore, siblingRevisionCountAfter);
        }
        finally { await CleanupAsync(requestId); }
    }

    // ── LEGACY-AMD-A/B (operational-contact consistency fix): a proposal that reached PENDING_APPROVAL
    // BEFORE this fix shipped is constructed here by writing the amendment/change rows DIRECTLY — the
    // real Submit path refuses both shapes outright now, so this is the only way to simulate "this
    // predates the deploy". Both must fail closed at Approve with the dedicated resubmit-required code,
    // never silently applied and never fuzzy-upgraded. ──

    /// <summary>
    /// LEGACY-AMD-A: the OLD "relationship-only" proposal shape — a change row directly naming
    /// <see cref="VisitFieldClassifier.OperationalContactGuestMemberId"/> — reaching Approve today.
    /// </summary>
    [Fact]
    public async Task LegacyAmdA_a_direct_relation_change_row_fails_closed_at_approve()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(32));
            var (guestA, guestB) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
            }

            ulong amendmentId; ulong hostA; ulong campusA;
            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceA);
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                // SeedTwoGuestMembersAsync already filed and approved its own amendment #1 on this
                // instance — the unique (VisitInstanceId, AmendmentNo) index means this one must follow it.
                var nextAmendmentNo = (await db.VisitInstanceAmendments.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceA).MaxAsync(a => (uint?)a.AmendmentNo) ?? 0) + 1;
                var amendment = new VisitInstanceAmendment
                {
                    VisitRequestId = requestId,
                    VisitInstanceId = instanceA,
                    AmendmentNo = nextAmendmentNo,
                    Status = AmendmentStatuses.PendingApproval,
                    BaseFormRevision = detail.FormRevision,
                    BaseApprovalRevision = detail.ApprovalRevision,
                    RequestedBy = Registrant,
                    RequestedAt = Now,
                    ExpiresAt = instance.PlannedStartAt.AddHours(-24),
                    ExpectedInstanceRowVersion = (uint)instance.RowVersion,
                    CreatedAt = Now,
                };
                amendment.Changes.Add(new VisitInstanceAmendmentChange
                {
                    FieldPath = VisitFieldClassifier.OperationalContactGuestMemberId,
                    ChangeClass = AmendmentChangeClasses.ApprovalSensitive,
                    OldValueJson = JsonSerializer.Serialize(guestA),
                    NewValueJson = JsonSerializer.Serialize(guestB), // the old-contract "pick a different existing member" shape
                    IsSensitive = true,
                    DisplayOrder = 0,
                    CreatedAt = Now,
                });
                db.VisitInstanceAmendments.Add(amendment);
                await db.SaveChangesAsync();
                amendmentId = amendment.AmendmentId;
                hostA = instance.CurrentHostUserId!.Value;
                campusA = instance.CampusId;
            }

            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentLegacyContactRelationRequiresResubmission, ex.ErrorCode);
            }

            using var db2 = NewContext();
            var detailAfter = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Equal(guestA, detailAfter.OperationalContactGuestMemberId); // untouched
            var amendmentAfter = await db2.VisitInstanceAmendments.AsNoTracking().SingleAsync(a => a.AmendmentId == amendmentId);
            Assert.Equal(AmendmentStatuses.PendingApproval, amendmentAfter.Status); // never consumed
        }
        finally { await CleanupAsync(requestId); }
    }

    /// <summary>
    /// LEGACY-AMD-B: the member-list JSON predates the <c>GuestMemberId</c> wire field entirely — every
    /// row deserializes with it null, the structural shape of a pre-deployment stored proposal.
    /// </summary>
    [Fact]
    public async Task LegacyAmdB_a_pre_deployment_member_list_json_with_no_GuestMemberId_evidence_fails_closed_at_approve()
    {
        RequireDb();
        ulong requestId = 0;
        try
        {
            (requestId, var instanceA, _) = await CreateApprovedAsync(Now.AddDays(33));
            var (guestA, _) = await SeedTwoGuestMembersAsync(requestId, instanceA);
            using (var db = NewContext())
            {
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                detail.OperationalContactGuestMemberId = guestA;
                await db.SaveChangesAsync();
            }

            // The OLD wire shape: a JSON array of visitor objects with no "guestMemberId" property at
            // all — exactly what deserializing a pre-fix stored proposal produces today.
            var legacyVisitorsJson = "[{\"fullName\":\"Guest A (đổi tên)\",\"nationality\":\"VN\","
                + "\"jobTitle\":\"Guest\",\"organization\":\"GuestOrg\",\"clientMemberKey\":\"a-key\"}]";

            ulong amendmentId; ulong hostA; ulong campusA;
            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.SingleAsync(c => c.VisitInstanceId == instanceA);
                var detail = await db.VisitInstanceFormDetails.SingleAsync(d => d.VisitInstanceId == instanceA);
                // SeedTwoGuestMembersAsync already filed and approved its own amendment #1 on this
                // instance — the unique (VisitInstanceId, AmendmentNo) index means this one must follow it.
                var nextAmendmentNo = (await db.VisitInstanceAmendments.AsNoTracking()
                    .Where(a => a.VisitInstanceId == instanceA).MaxAsync(a => (uint?)a.AmendmentNo) ?? 0) + 1;
                var amendment = new VisitInstanceAmendment
                {
                    VisitRequestId = requestId,
                    VisitInstanceId = instanceA,
                    AmendmentNo = nextAmendmentNo,
                    Status = AmendmentStatuses.PendingApproval,
                    BaseFormRevision = detail.FormRevision,
                    BaseApprovalRevision = detail.ApprovalRevision,
                    RequestedBy = Registrant,
                    RequestedAt = Now,
                    ExpiresAt = instance.PlannedStartAt.AddHours(-24),
                    ExpectedInstanceRowVersion = (uint)instance.RowVersion,
                    CreatedAt = Now,
                };
                amendment.Changes.Add(new VisitInstanceAmendmentChange
                {
                    FieldPath = VisitFieldClassifier.Visitors,
                    ChangeClass = AmendmentChangeClasses.Structural,
                    OldValueJson = JsonSerializer.Serialize(new[] { new { fullName = "Guest A", nationality = "VN", jobTitle = "Guest", organization = "GuestOrg" } }),
                    NewValueJson = legacyVisitorsJson,
                    IsSensitive = true,
                    DisplayOrder = 0,
                    CreatedAt = Now,
                });
                amendment.Changes.Add(new VisitInstanceAmendmentChange
                {
                    FieldPath = VisitFieldClassifier.OperationalContactMemberKey,
                    ChangeClass = AmendmentChangeClasses.ApprovalSensitive,
                    OldValueJson = null,
                    NewValueJson = JsonSerializer.Serialize("a-key"),
                    IsSensitive = true,
                    DisplayOrder = 1,
                    CreatedAt = Now,
                });
                db.VisitInstanceAmendments.Add(amendment);
                await db.SaveChangesAsync();
                amendmentId = amendment.AmendmentId;
                hostA = instance.CurrentHostUserId!.Value;
                campusA = instance.CampusId;
            }

            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Decide(db, new FakeUser(hostA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, "OK"), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentLegacyContactRelationRequiresResubmission, ex.ErrorCode);
            }

            using var db2 = NewContext();
            var detailAfter = await db2.VisitInstanceFormDetails.AsNoTracking().SingleAsync(d => d.VisitInstanceId == instanceA);
            Assert.Equal(guestA, detailAfter.OperationalContactGuestMemberId); // untouched
            Assert.Equal("Guest A", (await db2.VisitGuestMembers.AsNoTracking()
                .SingleAsync(m => m.GuestMemberId == guestA)).FullName); // no COW rewrite happened
            var amendmentAfter = await db2.VisitInstanceAmendments.AsNoTracking().SingleAsync(a => a.AmendmentId == amendmentId);
            Assert.Equal(AmendmentStatuses.PendingApproval, amendmentAfter.Status); // never consumed
        }
        finally { await CleanupAsync(requestId); }
    }
}
