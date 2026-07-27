using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.Common.Exceptions;
using PEMS.Application.DepartmentLeaderPersonnel.Commands.ChangePersonnelStatus;
using PEMS.Application.DepartmentLeaderPersonnel.Common;
using PEMS.Domain.Constants;
using PEMS.UnitTests.TestInfrastructure;
using Xunit;

namespace PEMS.UnitTests.DepartmentLeaderPersonnel;

/// <summary>
/// Enable/disable (spec §15). The endpoint is deliberately narrow — ACTIVE ↔ INACTIVE only — because
/// the other two states have security meaning: PENDING activates by confirming its email, and LOCKED
/// needs the dedicated unlock flow. Neither may be reached through a management toggle.
/// </summary>
public class ChangePersonnelStatusCommandTests
{
    private const ulong TargetId = 901;

    private static ChangePersonnelStatusCommandHandler Handler(DepartmentLeaderTestHarness h)
        => new(h.Db, h.Scope, h.Locks, h.Sessions, h.Email.Object, h.Clock);

    private static Task<ChangePersonnelStatusResponse> Run(
        DepartmentLeaderTestHarness h, string targetStatus, ulong userId = TargetId, string? reason = null)
        => Handler(h).Handle(
            new ChangePersonnelStatusCommand { UserId = userId, TargetStatus = targetStatus, Reason = reason },
            CancellationToken.None);

    [Fact]
    public async Task Active_to_inactive_disables_and_revokes_every_session()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Active);
        h.AddActiveSession(5001, TargetId);
        h.AddActiveSession(5002, TargetId);

        var result = await Run(h, UserStatuses.Inactive, reason: "Nhân sự đã chuyển công tác.");

        Assert.Equal(UserStatuses.Inactive, h.GetUser(TargetId).Status);
        Assert.Equal(UserStatuses.Active, result.PreviousStatus);
        Assert.Equal(2, result.RevokedSessions);

        var call = Assert.Single(h.Sessions.RevokeAllCalls);
        Assert.Equal(SessionRevokeReasons.AccountDeactivated, call.Reason);
    }

    [Fact]
    public async Task Inactive_to_active_enables_without_restoring_sessions()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Inactive);

        var result = await Run(h, UserStatuses.Active);

        Assert.Equal(UserStatuses.Active, h.GetUser(TargetId).Status);
        Assert.Equal(0, result.RevokedSessions);
        // Enabling grants nothing back automatically — the user signs in again for a fresh token.
        Assert.Empty(h.Sessions.RevokeAllCalls);
        Assert.Contains("đăng nhập lại", result.Message);
    }

    [Fact]
    public async Task Disable_does_not_delete_the_user_or_remove_them_from_the_department()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Active);

        await Run(h, UserStatuses.Inactive);

        var target = h.GetUser(TargetId);
        Assert.NotNull(target);
        Assert.Equal(DepartmentLeaderTestHarness.DepartmentId, target.DepartmentId);
        Assert.Equal(UserSubRoles.Staff, target.SubRole);
    }

    [Fact]
    public async Task Leader_cannot_disable_their_own_account()
    {
        var h = DepartmentLeaderTestHarness.Create();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Run(h, UserStatuses.Inactive, userId: DepartmentLeaderTestHarness.LeaderId));

        Assert.Equal(DepartmentLeaderErrorCodes.PersonnelSelfDisableForbidden, ex.ErrorCode);
        Assert.Equal(UserStatuses.Active, h.GetUser(DepartmentLeaderTestHarness.LeaderId).Status);
    }

    /// <summary>
    /// Disabling the seated head would leave the department without one, so it is refused. Note that
    /// the scope gate only admits a caller who IS the current head, so "target is the head" and
    /// "target is me" always coincide here — both blockers are raised, and the self-disable one is
    /// reported first. The head-specific rule is kept as defence in depth (and is what the impact
    /// preview surfaces), not as dead weight.
    /// </summary>
    [Fact]
    public async Task Current_department_head_cannot_be_disabled()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var headId = DepartmentLeaderTestHarness.LeaderId;
        Assert.Equal(headId, h.GetDepartment().HeadUserId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => Run(h, UserStatuses.Inactive, userId: headId));

        Assert.Equal(DepartmentLeaderErrorCodes.PersonnelSelfDisableForbidden, ex.ErrorCode);
        Assert.Equal(UserStatuses.Active, h.GetUser(headId).Status);
        Assert.Equal(headId, h.GetDepartment().HeadUserId);
    }

    /// <summary>
    /// The head-specific blocker itself, exercised directly against the shared rules module (which
    /// the impact preview also calls), independently of the caller-is-the-head coupling above.
    /// </summary>
    [Fact]
    public async Task Status_rules_flag_the_seated_head_with_its_own_blocker_code()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var target = h.AddStaff(TargetId, status: UserStatuses.Active);
        var scope = await h.Scope.EnsureCurrentUserIsActualDepartmentLeaderAsync(CancellationToken.None);

        var impact = await DepartmentPersonnelStatusRules.EvaluateAsync(
            h.Db, scope, target, UserStatuses.Inactive,
            EntityStatuses.Active, EntityStatuses.Active,
            departmentHeadUserId: TargetId,          // pretend this member holds the seat
            h.Clock.VietnamNow, CancellationToken.None);

        Assert.False(impact.CanChangeStatus);
        Assert.Contains(
            impact.Blockers,
            b => b.Code == DepartmentLeaderErrorCodes.CurrentLeaderDisableForbidden);
    }

    [Fact]
    public async Task Pending_account_cannot_be_activated_through_the_status_toggle()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.PendingEmailConfirmation);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Run(h, UserStatuses.Active));

        // Activating here would bypass the email-ownership proof entirely.
        Assert.Equal(DepartmentLeaderErrorCodes.PersonnelEmailConfirmationPending, ex.ErrorCode);
        Assert.Equal(UserStatuses.PendingEmailConfirmation, h.GetUser(TargetId).Status);
    }

    [Fact]
    public async Task Locked_account_cannot_be_activated_through_the_status_toggle()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Locked);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Run(h, UserStatuses.Active));

        Assert.Equal(DepartmentLeaderErrorCodes.PersonnelSecurityLocked, ex.ErrorCode);
        Assert.Equal(UserStatuses.Locked, h.GetUser(TargetId).Status);
    }

    [Fact]
    public async Task Locked_account_cannot_be_disabled_here_either()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Locked);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Run(h, UserStatuses.Inactive));

        Assert.Equal(DepartmentLeaderErrorCodes.PersonnelSecurityLocked, ex.ErrorCode);
        Assert.Equal(UserStatuses.Locked, h.GetUser(TargetId).Status);
    }

    [Fact]
    public async Task Unsupported_target_status_is_refused()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Active);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Run(h, UserStatuses.Locked));

        Assert.Equal(DepartmentLeaderErrorCodes.PersonnelInvalidStatus, ex.ErrorCode);
        Assert.Equal(UserStatuses.Active, h.GetUser(TargetId).Status);
    }

    /// <summary>
    /// An account still holding an open responsibility is refused with 409 and the blocker breakdown,
    /// rather than being disabled with its work silently orphaned or auto-reassigned (spec §14).
    /// </summary>
    [Fact]
    public async Task Active_responsibilities_block_the_disable_with_a_409_and_a_blocker_list()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Active);

        // BEFORE_VISIT is one of the statuses that keep a responsibility alive.
        h.Db.VisitRequestCampuses.Add(
            Uc106TestData.CreateVisitInstance(300, VisitInstanceStatuses.BeforeVisit));
        h.Db.VisitParticipants.Add(Uc106TestData.CreateDeptSupportParticipant(
            participantId: 400, visitInstanceId: 300, userId: TargetId, status: ParticipantStatuses.Accepted));
        h.Db.SaveChanges();
        h.Detach();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Run(h, UserStatuses.Inactive));

        Assert.Equal(DepartmentLeaderErrorCodes.PersonnelHasActiveResponsibilities, ex.ErrorCode);
        Assert.NotNull(ex.Data);
        // A blocked change leaves the database exactly as it found it.
        Assert.Equal(UserStatuses.Active, h.GetUser(TargetId).Status);
        Assert.Empty(h.Sessions.RevokeAllCalls);
    }

    [Fact]
    public async Task Target_in_another_department_answers_404()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddOtherDepartment();
        h.AddStaff(
            TargetId,
            departmentId: DepartmentLeaderTestHarness.OtherDepartmentId,
            campusId: DepartmentLeaderTestHarness.OtherCampusId);

        var ex = await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h, UserStatuses.Inactive));

        Assert.Equal(404, ex.StatusCode);
        Assert.Equal(UserStatuses.Active, h.GetUser(TargetId).Status);
    }

    [Fact]
    public async Task Enable_is_blocked_when_the_department_is_inactive()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Inactive);
        var department = h.GetDepartment();
        department.Status = EntityStatuses.Inactive;
        h.Db.SaveChanges();
        h.Detach();

        // The scope gate refuses an inactive department outright; the member stays inactive either way.
        await Assert.ThrowsAsync<AuthBusinessException>(() => Run(h, UserStatuses.Active));
        Assert.Equal(UserStatuses.Inactive, h.GetUser(TargetId).Status);
    }

    [Fact]
    public async Task Status_change_is_audited_with_the_reason()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Active);

        await Run(h, UserStatuses.Inactive, reason: "Chuyển công tác");

        var audit = h.Db.AuditLogs.Single();
        Assert.Equal(DepartmentPersonnelAuditActions.DisablePersonnel, audit.Action);
        Assert.Equal(UserStatuses.Active, audit.Changes.Single().OldValueText);

        // Parse rather than substring-match: the serializer escapes Vietnamese diacritics, so the
        // raw JSON holds "Chuyển" and a naive Contains would fail on correct data.
        using var payload = JsonDocument.Parse(audit.Changes.Single().NewValueText!);
        Assert.Equal("Chuyển công tác", payload.RootElement.GetProperty("reason").GetString());
        Assert.Equal(UserStatuses.Inactive, payload.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Enable_uses_its_own_audit_action_and_clears_temporary_lockout_counters()
    {
        var h = DepartmentLeaderTestHarness.Create();
        var target = h.AddStaff(TargetId, status: UserStatuses.Inactive);
        target.FailedLoginCount = 3;
        h.Db.SaveChanges();
        h.Detach();

        await Run(h, UserStatuses.Active);

        Assert.Equal(DepartmentPersonnelAuditActions.EnablePersonnel, h.Db.AuditLogs.Single().Action);
        Assert.Equal(0, h.GetUser(TargetId).FailedLoginCount);
    }

    [Fact]
    public async Task Rows_are_locked_before_the_write()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(TargetId, status: UserStatuses.Active);

        await Run(h, UserStatuses.Inactive);

        Assert.Contains(h.Locks.AllLockedUserIds, id => id == TargetId);
        Assert.Contains(
            h.Locks.LockedDepartmentBatches,
            batch => batch.Contains(DepartmentLeaderTestHarness.DepartmentId));
    }
}
