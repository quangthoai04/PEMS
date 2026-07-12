using PEMS.Application.Departments.Common;
using PEMS.Domain.Constants;

namespace PEMS.UnitTests.Departments.ManageDepartmentStatus;

/// <summary>
/// Unit tests for <see cref="DepartmentParticipantDependencyRule"/> — the UC-106 participant
/// blocker matrix (BR-UC106-24..39, doc §20 groups A–E). Pure logic, no database: the rule is the
/// single classification the impact calculator applies to joined participant rows.
/// </summary>
public class DepartmentParticipantDependencyRuleTests
{
    private const ulong TargetDepartmentId = 10;
    private const ulong OtherDepartmentId = 20;

    private static ParticipantDependencyCandidate Candidate(
        string participantStatus,
        string visitStatus,
        string roleCode = RoleCodes.Department,
        string? subRole = UserSubRoles.Staff,
        string participantRole = ParticipantRoles.DeptSupport,
        ulong? userDepartmentId = TargetDepartmentId,
        ulong userId = 100,
        ulong visitInstanceId = 500) => new()
    {
        UserId = userId,
        VisitInstanceId = visitInstanceId,
        UserDepartmentId = userDepartmentId,
        RoleCode = roleCode,
        SubRole = subRole,
        ParticipantRole = participantRole,
        ParticipantStatus = participantStatus,
        VisitStatus = visitStatus,
    };

    private static IReadOnlyList<DepartmentStatusBlocker> Build(params ParticipantDependencyCandidate[] candidates)
        => DepartmentParticipantDependencyRule.BuildBlockers(candidates, TargetDepartmentId);

    // ── Group A — participant status matrix (doc §20 tests 1–8) ─────────────

    [Fact]
    public void Leader_Invited_OnAssignedVisit_IsPendingInvitationBlocker()
    {
        var blockers = Build(Candidate(ParticipantStatuses.Invited, VisitInstanceStatuses.Assigned,
            subRole: UserSubRoles.Leader));

        var blocker = Assert.Single(blockers);
        Assert.Equal(DepartmentParticipantDependencyRule.PendingInvitationsBlockerType, blocker.Type);
        Assert.Equal(1, blocker.Count);
    }

    [Fact]
    public void Staff_Invited_OnBeforeVisit_Blocks()
    {
        var blockers = Build(Candidate(ParticipantStatuses.Invited, VisitInstanceStatuses.BeforeVisit));

        var blocker = Assert.Single(blockers);
        Assert.Equal(DepartmentParticipantDependencyRule.PendingInvitationsBlockerType, blocker.Type);
    }

    [Fact]
    public void Leader_Accepted_OnBeforeVisit_IsActiveParticipationBlocker()
    {
        var blockers = Build(Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.BeforeVisit,
            subRole: UserSubRoles.Leader));

        var blocker = Assert.Single(blockers);
        Assert.Equal(DepartmentParticipantDependencyRule.ActiveParticipationsBlockerType, blocker.Type);
    }

    [Fact]
    public void Staff_Accepted_OnDuringVisit_Blocks()
    {
        var blockers = Build(Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.DuringVisit));

        var blocker = Assert.Single(blockers);
        Assert.Equal(DepartmentParticipantDependencyRule.ActiveParticipationsBlockerType, blocker.Type);
    }

    [Theory]
    [InlineData("ASSIGNED")]   // doc test 5: staff ASSIGNED on visit ASSIGNED
    [InlineData("AFTER_VISIT")] // doc test 6: staff ASSIGNED on AFTER_VISIT (post-visit duties remain)
    public void Staff_Assigned_OnOperationalVisit_Blocks(string visitStatus)
    {
        var blockers = Build(Candidate(ParticipantStatuses.Assigned, visitStatus));

        var blocker = Assert.Single(blockers);
        Assert.Equal(DepartmentParticipantDependencyRule.ActiveParticipationsBlockerType, blocker.Type);
    }

    [Theory]
    [InlineData("DECLINED")] // doc test 7: no remaining obligation
    [InlineData("REMOVED")]  // doc test 8: removed from the visit
    public void DeclinedOrRemoved_OnOperationalVisit_DoesNotBlock(string participantStatus)
    {
        var blockers = Build(
            Candidate(participantStatus, VisitInstanceStatuses.Assigned),
            Candidate(participantStatus, VisitInstanceStatuses.DuringVisit, visitInstanceId: 501));

        Assert.Empty(blockers);
    }

    // ── Group B — visit status matrix (doc §20 tests 9–16) ──────────────────

    [Theory]
    [InlineData("INVITED", "ASSIGNED")]      // 9
    [InlineData("INVITED", "BEFORE_VISIT")]  // 10
    [InlineData("ACCEPTED", "DURING_VISIT")] // 11
    [InlineData("ACCEPTED", "AFTER_VISIT")]  // 12
    public void BlockingParticipant_OnOperationalVisit_Blocks(string participantStatus, string visitStatus)
    {
        var blockers = Build(Candidate(participantStatus, visitStatus));

        Assert.Single(blockers);
    }

    [Theory]
    [InlineData("ACCEPTED", "CLOSED")]    // 13
    [InlineData("ASSIGNED", "CANCELLED")] // 14
    [InlineData("INVITED", "REJECTED")]   // 15
    public void BlockingParticipant_OnTerminalVisit_DoesNotBlock(string participantStatus, string visitStatus)
    {
        var blockers = Build(Candidate(participantStatus, visitStatus));

        Assert.Empty(blockers);
    }

    [Fact]
    public void Invited_OnWaitingRequestApproval_IsNotACanonicalBlocker()
    {
        // BR-UC106-32: the visit is not approved yet, so a participant row here is a data
        // anomaly, never a business blocker (no anomaly-warning abstraction exists in source).
        var blockers = Build(Candidate(ParticipantStatuses.Invited, VisitInstanceStatuses.WaitingRequestApproval));

        Assert.Empty(blockers);
    }

    [Fact]
    public void OperationalAllowlist_IsExplicit_NotDerivedFromTerminalStatuses()
    {
        // BR-UC106-33: allowlist must be exactly the four operational statuses.
        Assert.Equal(
            new[] { "ASSIGNED", "BEFORE_VISIT", "DURING_VISIT", "AFTER_VISIT" },
            DepartmentParticipantDependencyRule.OperationalVisitStatuses);
    }

    // ── Group C — department / role / participant-role scope (doc §20 tests 17–23) ──

    [Fact]
    public void UserOfTargetDepartment_IsConsidered()
    {
        var blockers = Build(Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned,
            userDepartmentId: TargetDepartmentId));

        Assert.Single(blockers);
    }

    [Fact]
    public void UserOfOtherDepartment_DoesNotBlock()
    {
        var blockers = Build(Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned,
            userDepartmentId: OtherDepartmentId));

        Assert.Empty(blockers);
    }

    [Fact]
    public void StaffRoleUser_WithAnomalousDepartmentId_DoesNotBlock()
    {
        // doc test 19: even if abnormal data gives a STAFF user the target department_id,
        // only role_code = DEPARTMENT counts.
        var blockers = Build(Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned,
            roleCode: RoleCodes.Staff));

        Assert.Empty(blockers);
    }

    [Fact]
    public void StudentRoleUser_DoesNotBlock()
    {
        var blockers = Build(Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned,
            roleCode: RoleCodes.Student, subRole: null));

        Assert.Empty(blockers);
    }

    [Theory]
    [InlineData("IC_SUPPORT")] // 21
    [InlineData("IC_HOST")]    // 22
    [InlineData("STUDENT")]    // 23
    public void NonDeptSupportParticipantRole_DoesNotBlock(string participantRole)
    {
        var blockers = Build(Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned,
            participantRole: participantRole));

        Assert.Empty(blockers);
    }

    // ── Group D — ASSIGNED canonical rule (doc §20 tests 24–25) ─────────────

    [Fact]
    public void DepartmentStaff_Assigned_IsCanonicalBlocker()
    {
        var blockers = Build(Candidate(ParticipantStatuses.Assigned, VisitInstanceStatuses.BeforeVisit,
            subRole: UserSubRoles.Staff));

        var blocker = Assert.Single(blockers);
        Assert.Equal(DepartmentParticipantDependencyRule.ActiveParticipationsBlockerType, blocker.Type);
    }

    [Fact]
    public void DepartmentLeader_Assigned_AnomalyStillBlocksDefensively()
    {
        // BR-UC106-29: ASSIGNED is not canonical for DEPARTMENT+LEADER, but if the row exists
        // it must still block — never silently skipped.
        var blockers = Build(Candidate(ParticipantStatuses.Assigned, VisitInstanceStatuses.BeforeVisit,
            subRole: UserSubRoles.Leader));

        var blocker = Assert.Single(blockers);
        Assert.Equal(DepartmentParticipantDependencyRule.ActiveParticipationsBlockerType, blocker.Type);
    }

    // ── Group E — count & grouping (doc §20 tests 26–30, BR-UC106-38) ───────

    [Fact]
    public void OneUser_ThreeVisits_CountsRecordsUsersAndVisitsSeparately()
    {
        var blockers = Build(
            Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned, userId: 100, visitInstanceId: 501),
            Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.BeforeVisit, userId: 100, visitInstanceId: 502),
            Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.DuringVisit, userId: 100, visitInstanceId: 503));

        var blocker = Assert.Single(blockers);
        Assert.Equal(3, blocker.Count);
        Assert.Equal(1, blocker.AffectedUserCount);
        Assert.Equal(3, blocker.AffectedVisitCount);
    }

    [Fact]
    public void TwoUsers_SameVisit_CountsRecordsUsersAndVisitsSeparately()
    {
        var blockers = Build(
            Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned, userId: 100, visitInstanceId: 501),
            Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned, userId: 101, visitInstanceId: 501));

        var blocker = Assert.Single(blockers);
        Assert.Equal(2, blocker.Count);
        Assert.Equal(2, blocker.AffectedUserCount);
        Assert.Equal(1, blocker.AffectedVisitCount);
    }

    [Fact]
    public void OneInvited_TwoAccepted_SplitsIntoBothBlockerTypes()
    {
        var blockers = Build(
            Candidate(ParticipantStatuses.Invited, VisitInstanceStatuses.Assigned, userId: 100, visitInstanceId: 501),
            Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.Assigned, userId: 101, visitInstanceId: 502),
            Candidate(ParticipantStatuses.Accepted, VisitInstanceStatuses.BeforeVisit, userId: 102, visitInstanceId: 503));

        Assert.Equal(2, blockers.Count);
        var pending = Assert.Single(blockers,
            b => b.Type == DepartmentParticipantDependencyRule.PendingInvitationsBlockerType);
        Assert.Equal(1, pending.Count);
        var active = Assert.Single(blockers,
            b => b.Type == DepartmentParticipantDependencyRule.ActiveParticipationsBlockerType);
        Assert.Equal(2, active.Count);
    }

    [Fact]
    public void OnlyDeclinedAndRemoved_ReturnsEmptyBlockerList()
    {
        var blockers = Build(
            Candidate(ParticipantStatuses.Declined, VisitInstanceStatuses.Assigned, userId: 100, visitInstanceId: 501),
            Candidate(ParticipantStatuses.Removed, VisitInstanceStatuses.DuringVisit, userId: 101, visitInstanceId: 502));

        Assert.Empty(blockers);
    }

    [Fact]
    public void NoParticipants_ReturnsEmptyBlockerList()
    {
        var blockers = Build();

        Assert.Empty(blockers);
    }
}
