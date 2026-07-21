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
            new ContactPointDto("Op Contact", "OpOrg", "+8410", "op@example.com"),
            "EN", "Xe 16 chỗ", "AGREED", null, "Ghi chú", null);

    /// <summary>Creates a committed 2-campus request and drives BOTH instances to ASSIGNED (parent APPROVED)
    /// using the real transition order. Returns (requestId, instanceA=HN, instanceB=HCM).</summary>
    private static async Task<(ulong RequestId, ulong InstanceA, ulong InstanceB)> CreateApprovedAsync(DateTime start)
    {
        ulong requestId;
        using (var db = NewContext())
        {
            var handler = new CreateVisitRequestV2CommandHandler(
                db, new FakeUser(Registrant), new FixedClock(), new VisitRequestV2CreateService(db),
                new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
                new UserProvisionService(db),
                NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
                    new VisitRequestAggregateStatusService(db));
            var form = new VisitRequestFormDataV2(
                "AM" + Guid.NewGuid().ToString("N"),
                new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
                new ContactPointDto("Registrant", "Org", "+8491", "registrant@example.com"),
                null, new List<CampusVisitFormDto> { Campus("HN", start), Campus("HCM", start.AddDays(1)) });
            requestId = (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
        }
        using (var db = NewContext())
        {
            var visit = await db.VisitRequests.Include(v => v.CampusInstances)
                .SingleAsync(v => v.VisitRequestId == requestId);
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
            new ContactPointDto(d.OperationalContactFullName ?? "", d.OperationalContactOrganization ?? "",
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
            // Empty diff (identical proposal) on the sibling → rejected.
            var identical = await BaselineProposalAsync(instanceB);
            using (var db = NewContext())
            {
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(requestId, instanceB, identical), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, ex.ErrorCode);
            }
        }
        finally { await CleanupAsync(requestId); }
    }

    [Fact]
    public async Task Approve_by_current_campus_leader_applies_target_only_and_never_resets_approval()
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
            ulong amendmentId; ulong leaderA;
            using (var db = NewContext())
            {
                var dto = await Submit(db, Registrant).Handle(
                    new SubmitVisitAmendmentCommand(requestId, instanceA, proposal), CancellationToken.None);
                amendmentId = dto.AmendmentId;
                leaderA = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceA).Select(c => c.CoordinatorUserId!.Value).SingleAsync();
            }

            // Wrong scope: another campus's leader / HO / the requester → forbidden.
            using (var db = NewContext())
            {
                var campusB = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceB).Select(c => c.CampusId).SingleAsync();
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Decide(db, new FakeUser(leaderA, RoleCodes.Staff, UserSubRoles.Leader, campusB)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, null), CancellationToken.None));
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Decide(db, new FakeUser(leaderA, RoleCodes.Ho)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, null), CancellationToken.None));
                await Assert.ThrowsAsync<ForbiddenException>(() =>
                    Decide(db, new FakeUser(Registrant)).Handle(
                        new ApproveVisitAmendmentCommand(instanceA, amendmentId, null), CancellationToken.None));
            }

            // Correct scope: the CURRENT Staff Leader of campus A.
            using (var db = NewContext())
            {
                var campusA = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitInstanceId == instanceA).Select(c => c.CampusId).SingleAsync();
                var res = await Decide(db, new FakeUser(leaderA, RoleCodes.Staff, UserSubRoles.Leader, campusA)).Handle(
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
                    new NoopNotifications(), new CreateVisitRequestV2CommandTests.RecordingClaimService(),
                    new UserProvisionService(db),
                    NullLogger<CreateVisitRequestV2CommandHandler>.Instance, ReadOn, WriteOn,
                    new VisitRequestAggregateStatusService(db));
                var form = new VisitRequestFormDataV2(
                    "AM" + Guid.NewGuid().ToString("N"),
                    new RegistrantInputV2("Registrant", "VN", "Org", "Job", "+8491", "registrant@example.com"),
                    new ContactPointDto("Registrant", "Org", "+8491", "registrant@example.com"),
                    null, new List<CampusVisitFormDto> { Campus("HN", Now.AddDays(20)) });
                pendingRequest = (await handler.Handle(new CreateVisitRequestV2Command(form), CancellationToken.None)).VisitRequestId;
            }
            using (var db = NewContext())
            {
                var instance = await db.VisitRequestCampuses.AsNoTracking()
                    .Where(c => c.VisitRequestId == pendingRequest).Select(c => c.VisitInstanceId).SingleAsync();
                var proposal = await BaselineProposalAsync(instance, b => b.Purpose = "Đổi");
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(pendingRequest, instance, proposal), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentNotEditable, ex.ErrorCode);
            }

            // A decided instance starting <24h from now → self-service window closed.
            (lateRequest, var lateInstance, _) = await CreateApprovedAsync(Now.AddHours(10));
            using (var db = NewContext())
            {
                var proposal = await BaselineProposalAsync(lateInstance, b => b.Purpose = "Đổi gấp");
                var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                    Submit(db, Registrant).Handle(
                        new SubmitVisitAmendmentCommand(lateRequest, lateInstance, proposal), CancellationToken.None));
                Assert.Equal(VisitFormV2ErrorCodes.AmendmentWindowExpired, ex.ErrorCode);
            }
        }
        finally
        {
            await CleanupAsync(pendingRequest);
            await CleanupAsync(lateRequest);
        }
    }
}
