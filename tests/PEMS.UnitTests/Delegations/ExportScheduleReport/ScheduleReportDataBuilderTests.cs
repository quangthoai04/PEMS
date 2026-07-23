using Microsoft.Extensions.Logging.Abstractions;
using PEMS.Application.Delegations.Queries.ExportScheduleReport;
using PEMS.Application.Delegations.Services.VisitFormRead;
using PEMS.Domain.Constants;
using Xunit;

namespace PEMS.UnitTests.Delegations.ExportScheduleReport;

public class ScheduleReportDataBuilderTests
{
    private static VisitFormReadService FormReadService(ScheduleReportTestDbContext db, FakeScheduleReportCurrentUser currentUser)
        => new(db, currentUser, NullLogger<VisitFormReadService>.Instance);

    [Fact]
    public async Task BuildAsync_maps_time_location_and_purpose_from_the_registration_form()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var (instance, _) = ScheduleReportTestData.SeedBase(db);
        var currentUser = new FakeScheduleReportCurrentUser();

        var dto = await ScheduleReportDataBuilder.BuildAsync(db, FormReadService(db, currentUser), instance, default);

        Assert.Equal(instance.PlannedStartAt, dto.PlannedStartAt);
        Assert.Equal(instance.PlannedEndAt, dto.PlannedEndAt);
        Assert.Equal("FPT University", dto.Location);
        Assert.Equal("Tham quan và ký kết hợp tác", dto.Purpose);
        Assert.Equal("Đoàn khách kiểm thử", dto.DelegationName);
    }

    [Fact]
    public async Task BuildAsync_merges_guest_members_and_external_support_into_one_guest_side_list()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var (instance, _) = ScheduleReportTestData.SeedBase(db);
        db.VisitGuestMembers.AddRange(
            ScheduleReportTestData.CreateGuestMember(1, "Trưởng đoàn khách", "GUEST", 0),
            ScheduleReportTestData.CreateGuestMember(2, "Thành viên đoàn", "GUEST", 1),
            ScheduleReportTestData.CreateGuestMember(3, "Phiên dịch hỗ trợ", "EXTERNAL_SUPPORT", 0));
        db.SaveChanges();
        var currentUser = new FakeScheduleReportCurrentUser();

        var dto = await ScheduleReportDataBuilder.BuildAsync(db, FormReadService(db, currentUser), instance, default);

        Assert.Equal(3, dto.GuestSide.Count);
        Assert.Contains(dto.GuestSide, p => p.FullName == "Trưởng đoàn khách" && p.RoleLabel == "Khách mời");
        Assert.Contains(dto.GuestSide, p => p.FullName == "Phiên dịch hỗ trợ" && p.RoleLabel == "Nhân sự hỗ trợ");
    }

    [Fact]
    public async Task BuildAsync_fpt_side_includes_host_and_accepted_participants_only()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var (instance, _) = ScheduleReportTestData.SeedBase(db);
        var icDept = db.Departments.First();
        db.Users.AddRange(
            ScheduleReportTestData.CreateUser(101, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, icDept.DepartmentId),
            ScheduleReportTestData.CreateUser(102, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, icDept.DepartmentId),
            ScheduleReportTestData.CreateUser(103, ScheduleReportTestData.StaffRoleId, UserSubRoles.Staff, icDept.DepartmentId));
        db.VisitParticipants.AddRange(
            // Accepted non-host support — must appear.
            ScheduleReportTestData.CreateParticipant(1, 101, ParticipantRoles.IcSupport, ParticipantStatuses.Accepted),
            // Invited but not yet responded — must NOT appear.
            ScheduleReportTestData.CreateParticipant(2, 102, ParticipantRoles.IcSupport, ParticipantStatuses.Invited),
            // Declined — must NOT appear.
            ScheduleReportTestData.CreateParticipant(3, 103, ParticipantRoles.DeptSupport, ParticipantStatuses.Declined));
        db.SaveChanges();
        var currentUser = new FakeScheduleReportCurrentUser();

        var dto = await ScheduleReportDataBuilder.BuildAsync(db, FormReadService(db, currentUser), instance, default);

        Assert.Equal(2, dto.FptSide.Count);
        Assert.Contains(dto.FptSide, p => p.FullName == "User 100" && p.RoleLabel == "Host");
        Assert.Contains(dto.FptSide, p => p.FullName == "User 101" && p.RoleLabel == "Cán bộ IC");
        Assert.DoesNotContain(dto.FptSide, p => p.FullName == "User 102");
        Assert.DoesNotContain(dto.FptSide, p => p.FullName == "User 103");
    }

    [Fact]
    public async Task BuildAsync_agenda_rows_are_ordered_and_fall_back_to_fpt_university_venue()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var (instance, _) = ScheduleReportTestData.SeedBase(db);
        db.VisitAgendas.AddRange(
            ScheduleReportTestData.CreateAgenda(2, "Ký kết hợp tác", new DateTime(2026, 8, 1, 10, 0, 0), sequenceOrder: 1, location: null),
            ScheduleReportTestData.CreateAgenda(1, "Đón tiếp tại sảnh", new DateTime(2026, 8, 1, 9, 0, 0), sequenceOrder: 0, location: "Sảnh A, Tòa Beta"));
        db.SaveChanges();
        var currentUser = new FakeScheduleReportCurrentUser();

        var dto = await ScheduleReportDataBuilder.BuildAsync(db, FormReadService(db, currentUser), instance, default);

        Assert.Equal(2, dto.Agenda.Count);
        Assert.Equal("Đón tiếp tại sảnh", dto.Agenda[0].Title);
        Assert.Equal("Sảnh A, Tòa Beta", dto.Agenda[0].Venue);
        Assert.Equal("Ký kết hợp tác", dto.Agenda[1].Title);
        Assert.Equal("FPT University", dto.Agenda[1].Venue);
    }

    [Fact]
    public async Task BuildAsync_sets_partner_logo_file_id_only_when_the_linked_partner_has_a_logo()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var partner = ScheduleReportTestData.CreatePartner(50, "Asia University", logoFileId: 900);
        db.Partners.Add(partner);
        db.SaveChanges();
        var (instance, _) = ScheduleReportTestData.SeedBase(db, partnerId: 50);
        var currentUser = new FakeScheduleReportCurrentUser();

        var dto = await ScheduleReportDataBuilder.BuildAsync(db, FormReadService(db, currentUser), instance, default);

        Assert.Equal((ulong)900, dto.PartnerLogoFileId);
        Assert.Equal("Asia University", dto.PartnerName);
    }

    [Fact]
    public async Task BuildAsync_leaves_partner_logo_file_id_null_for_a_new_partner_without_a_logo()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var partner = ScheduleReportTestData.CreatePartner(51, "New Partner", logoFileId: null);
        db.Partners.Add(partner);
        db.SaveChanges();
        var (instance, _) = ScheduleReportTestData.SeedBase(db, partnerId: 51);
        var currentUser = new FakeScheduleReportCurrentUser();

        var dto = await ScheduleReportDataBuilder.BuildAsync(db, FormReadService(db, currentUser), instance, default);

        Assert.Null(dto.PartnerLogoFileId);
    }

    [Fact]
    public async Task BuildAsync_leaves_partner_logo_file_id_null_when_the_request_has_no_partner()
    {
        using var db = ScheduleReportTestDbContext.Create();
        var (instance, _) = ScheduleReportTestData.SeedBase(db, partnerId: null);
        var currentUser = new FakeScheduleReportCurrentUser();

        var dto = await ScheduleReportDataBuilder.BuildAsync(db, FormReadService(db, currentUser), instance, default);

        Assert.Null(dto.PartnerLogoFileId);
    }
}
