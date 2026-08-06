using PEMS.Application.Delegations.Queries.GetVisitProcessPermissions;
using PEMS.Domain.Constants;
using PEMS.Shared;
using PEMS.UnitTests.Delegations.ExportScheduleReport;
using Xunit;

namespace PEMS.UnitTests.Delegations.SetupProgressEmail;

/// <summary>
/// Who may send "Gửi cập nhật chuẩn bị", as the process-permissions endpoint reports it.
///
/// <para>
/// The rule is deliberately narrower than "can see this page" and narrower than "can download the
/// report": Staff Leader and HO read the same screen and can pull the same PDF, but writing to the
/// guest AS the delegation's host is a different act. The flag is the only thing the frontend renders
/// the button from, so every one of those distinctions is asserted here rather than left to the UI.
/// </para>
/// </summary>
public class SetupProgressEmailPermissionTests
{
    private static (ScheduleReportTestDbContext Db, GetVisitProcessPermissionsQueryHandler Handler, FakeScheduleReportCurrentUser User)
        CreateSut(string instanceStatus = VisitInstanceStatus.BeforeVisit)
    {
        var db = ScheduleReportTestDbContext.Create();
        ScheduleReportTestData.SeedBase(db, instanceStatus);
        var user = new FakeScheduleReportCurrentUser();
        return (db, new GetVisitProcessPermissionsQueryHandler(db, user), user);
    }

    private static Task<VisitProcessPermissionDto> AskAsync(GetVisitProcessPermissionsQueryHandler handler) =>
        handler.Handle(new GetVisitProcessPermissionsQuery(ScheduleReportTestData.VisitInstanceId), default);

    [Theory]
    [InlineData(VisitInstanceStatus.BeforeVisit)]
    public async Task The_current_host_may_send_while_the_preparation_tab_is_open(string status)
    {
        var (_, handler, user) = CreateSut(status);
        user.UserId = ScheduleReportTestData.HostUserId;

        var perm = await AskAsync(handler);

        Assert.True(perm.CanSendSetupProgressEmail);
    }

    [Theory]
    [InlineData(VisitInstanceStatus.DuringVisit)]
    [InlineData(VisitInstanceStatus.AfterVisit)]
    [InlineData(VisitInstanceStatus.Closed)]
    [InlineData(VisitInstanceStatus.Cancelled)]
    public async Task The_host_may_not_send_once_the_preparation_window_has_passed(string status)
    {
        var (_, handler, user) = CreateSut(status);
        user.UserId = ScheduleReportTestData.HostUserId;

        var perm = await AskAsync(handler);

        // The mail describes preparation the Host can no longer change, so it stops being an update.
        Assert.False(perm.CanSendSetupProgressEmail);
    }

    [Fact]
    public async Task The_staff_leader_of_the_campus_may_not_send_although_they_may_read_the_page()
    {
        var (db, handler, user) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(200, ScheduleReportTestData.StaffRoleId, UserSubRoles.Leader, null));
        db.SaveChanges();
        user.UserId = 200;
        user.RoleCode = RoleCodes.Staff;
        user.SubRole = UserSubRoles.Leader;
        user.PrimaryCampusId = ScheduleReportTestData.CampusId;

        var perm = await AskAsync(handler);

        Assert.Equal("STAFF_LEADER", perm.Relation);
        Assert.True(perm.CanViewBeforeVisit);
        Assert.False(perm.CanSendSetupProgressEmail);
    }

    [Fact]
    public async Task Ho_may_not_send()
    {
        var (db, handler, user) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(300, ScheduleReportTestData.HoRoleId, null, null));
        db.SaveChanges();
        user.UserId = 300;
        user.RoleCode = RoleCodes.Ho;
        user.SubRole = null;
        user.PrimaryCampusId = null;

        var perm = await AskAsync(handler);

        Assert.False(perm.CanSendSetupProgressEmail);
    }

    [Fact]
    public async Task An_accepted_participant_may_not_send()
    {
        var (db, handler, user) = CreateSut();
        db.Users.Add(ScheduleReportTestData.CreateUser(400, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null));
        db.VisitParticipants.Add(ScheduleReportTestData.CreateParticipant(
            ScheduleReportTestData.VisitInstanceId, 400, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted));
        db.SaveChanges();
        user.UserId = 400;

        var perm = await AskAsync(handler);

        Assert.False(perm.CanSendSetupProgressEmail);
    }

    /// <summary>
    /// A handover moves the flag with it. This is the state the send command's re-authorisation
    /// exists for: the previous host may still be holding a draft this endpoint has already stopped
    /// endorsing.
    /// </summary>
    [Fact]
    public async Task A_replaced_host_loses_the_flag()
    {
        var (db, handler, user) = CreateSut();
        var instance = db.VisitRequestCampuses.Single(c => c.VisitInstanceId == ScheduleReportTestData.VisitInstanceId);
        db.Users.Add(ScheduleReportTestData.CreateUser(600, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, null));
        instance.CurrentHostUserId = 600;
        db.SaveChanges();

        // The old host keeps no relation to the instance at all, so they do not merely lose the
        // flag — the whole page stops answering them.
        user.UserId = ScheduleReportTestData.HostUserId;
        await Assert.ThrowsAsync<PEMS.Application.Common.Exceptions.ForbiddenException>(
            () => AskAsync(handler));

        user.UserId = 600;                                  // the NEW host
        var newHostPerm = await AskAsync(handler);
        Assert.True(newHostPerm.CanSendSetupProgressEmail);
    }
}
