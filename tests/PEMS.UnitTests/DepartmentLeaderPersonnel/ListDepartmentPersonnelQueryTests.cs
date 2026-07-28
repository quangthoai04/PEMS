using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PEMS.Application.DepartmentLeaderPersonnel.Queries.ListDepartmentPersonnel;
using PEMS.Domain.Constants;
using PEMS.Domain.Enums;
using Xunit;

namespace PEMS.UnitTests.DepartmentLeaderPersonnel;

/// <summary>
/// List / search / filter / sort / paging (spec §9). The containment property matters most: scope is
/// applied BEFORE the keyword, so no search term can surface a row from another department.
/// </summary>
public class ListDepartmentPersonnelQueryTests
{
    private static ListDepartmentPersonnelQueryHandler Handler(DepartmentLeaderTestHarness h)
        => new(h.Db, h.Scope);

    private static Task<ListDepartmentPersonnelResponse> Run(
        DepartmentLeaderTestHarness h, ListDepartmentPersonnelQuery query)
        => Handler(h).Handle(query, CancellationToken.None);

    [Fact]
    public async Task Returns_only_department_members_and_never_another_departments_rows()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(901, fullName: "An Nguyen");
        h.AddOtherDepartment();
        h.AddStaff(
            950,
            departmentId: DepartmentLeaderTestHarness.OtherDepartmentId,
            campusId: DepartmentLeaderTestHarness.OtherCampusId,
            fullName: "An Tran");

        var result = await Run(h, new ListDepartmentPersonnelQuery());

        Assert.Equal(2, result.TotalItems); // the leader + one staff member
        Assert.DoesNotContain(result.Items, i => i.UserId == 950);
    }

    /// <summary>
    /// The IDOR-adjacent case: a keyword that matches somebody in another department must still not
    /// return them, because scope is filtered first.
    /// </summary>
    [Fact]
    public async Task Keyword_cannot_reach_across_departments()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddOtherDepartment();
        h.AddStaff(
            950,
            departmentId: DepartmentLeaderTestHarness.OtherDepartmentId,
            campusId: DepartmentLeaderTestHarness.OtherCampusId,
            fullName: "Nguoi Phong Khac",
            email: "outsider@fpt.edu.vn");

        var result = await Run(h, new ListDepartmentPersonnelQuery { Keyword = "outsider" });

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalItems);
    }

    [Theory]
    [InlineData("bao")]        // name fragment
    [InlineData("BAO")]        // case-insensitive
    [InlineData("bao.staff")]  // email fragment
    [InlineData("0988")]       // phone fragment
    public async Task Keyword_matches_name_email_and_phone_case_insensitively(string keyword)
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(901, fullName: "Bao Nguyen", email: "bao.staff@fpt.edu.vn", phone: "0988123456");
        h.AddStaff(902, fullName: "Chi Tran", email: "chi@fpt.edu.vn", phone: "0912000000");

        var result = await Run(h, new ListDepartmentPersonnelQuery { Keyword = keyword });

        Assert.Single(result.Items);
        Assert.Equal(901ul, result.Items[0].UserId);
    }

    [Fact]
    public async Task Status_filter_keeps_inactive_and_locked_separate()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(901, status: UserStatuses.Inactive);
        h.AddStaff(902, status: UserStatuses.Locked);
        h.AddStaff(903, status: UserStatuses.PendingEmailConfirmation);

        var inactive = await Run(h, new ListDepartmentPersonnelQuery { Status = UserStatuses.Inactive });
        var locked = await Run(h, new ListDepartmentPersonnelQuery { Status = UserStatuses.Locked });
        var pending = await Run(h, new ListDepartmentPersonnelQuery
        {
            Status = UserStatuses.PendingEmailConfirmation,
        });

        Assert.Equal(901ul, Assert.Single(inactive.Items).UserId);
        Assert.Equal(902ul, Assert.Single(locked.Items).UserId);
        Assert.Equal(903ul, Assert.Single(pending.Items).UserId);
    }

    [Fact]
    public async Task Status_all_returns_every_status()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(901, status: UserStatuses.Inactive);
        h.AddStaff(902, status: UserStatuses.Locked);

        var result = await Run(h, new ListDepartmentPersonnelQuery { Status = "ALL" });

        Assert.Equal(3, result.TotalItems);
    }

    [Fact]
    public async Task Keyword_and_status_are_combined_with_and_not_or()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(901, fullName: "Bao Nguyen", status: UserStatuses.Active);
        h.AddStaff(902, fullName: "Bao Tran", status: UserStatuses.Inactive);

        var result = await Run(h, new ListDepartmentPersonnelQuery
        {
            Keyword = "bao",
            Status = UserStatuses.Inactive,
        });

        // OR would return both "Bao" rows; AND returns only the inactive one.
        Assert.Equal(902ul, Assert.Single(result.Items).UserId);
    }

    [Fact]
    public async Task Current_leader_is_listed_first_by_default()
    {
        var h = DepartmentLeaderTestHarness.Create();
        // Names that sort before the leader's "Truong Phong", to prove the pin is not alphabetical luck.
        h.AddStaff(901, fullName: "An Nguyen");
        h.AddStaff(902, fullName: "Binh Le");

        var result = await Run(h, new ListDepartmentPersonnelQuery());

        Assert.Equal(DepartmentLeaderTestHarness.LeaderId, result.Items[0].UserId);
        Assert.Equal("Trưởng phòng", result.Items[0].Position);
    }

    [Fact]
    public async Task Paging_reports_totals_and_navigation_flags()
    {
        var h = DepartmentLeaderTestHarness.Create();
        for (ulong id = 901; id <= 905; id++) h.AddStaff(id, fullName: $"Nhan Vien {id}");

        var firstPage = await Run(h, new ListDepartmentPersonnelQuery { Page = 1, PageSize = 2 });
        var lastPage = await Run(h, new ListDepartmentPersonnelQuery { Page = 3, PageSize = 2 });

        Assert.Equal(6, firstPage.TotalItems);   // 5 staff + leader
        Assert.Equal(3, firstPage.TotalPages);
        Assert.True(firstPage.HasNextPage);
        Assert.False(firstPage.HasPreviousPage);

        Assert.False(lastPage.HasNextPage);
        Assert.True(lastPage.HasPreviousPage);
    }

    /// <summary>
    /// Paging must be stable when several members share a name — the trailing user_id tiebreaker is
    /// what stops a row from being skipped entirely between page 1 and page 2.
    /// </summary>
    [Fact]
    public async Task Paging_is_stable_when_names_collide()
    {
        var h = DepartmentLeaderTestHarness.Create();
        for (ulong id = 901; id <= 904; id++) h.AddStaff(id, fullName: "Trung Ten");

        var page1 = await Run(h, new ListDepartmentPersonnelQuery { Page = 1, PageSize = 2 });
        var page2 = await Run(h, new ListDepartmentPersonnelQuery { Page = 2, PageSize = 2 });
        var page3 = await Run(h, new ListDepartmentPersonnelQuery { Page = 3, PageSize = 2 });

        var seen = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(i => i.UserId).ToList();
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(5, seen.Count); // leader + 4 staff, each exactly once
    }

    [Fact]
    public async Task Gender_is_emitted_as_the_canonical_wire_value_not_a_label_or_ordinal()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(901, gender: Gender.Female);
        h.AddStaff(902, gender: null);

        var result = await Run(h, new ListDepartmentPersonnelQuery());

        Assert.Equal("FEMALE", result.Items.First(i => i.UserId == 901).Gender);
        // Unset stays unset — it must not be coerced into OTHER.
        Assert.Null(result.Items.First(i => i.UserId == 902).Gender);
    }

    [Fact]
    public async Task Action_flags_reflect_status_and_seat()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff(901, status: UserStatuses.Active);
        h.AddStaff(902, status: UserStatuses.Inactive);
        h.AddStaff(903, status: UserStatuses.PendingEmailConfirmation);
        h.AddStaff(904, status: UserStatuses.Locked);

        var result = await Run(h, new ListDepartmentPersonnelQuery { PageSize = 50 });
        var byId = result.Items.ToDictionary(i => i.UserId);

        // The seated leader (also the caller) can never be disabled or chosen as their own successor.
        var leader = byId[DepartmentLeaderTestHarness.LeaderId];
        Assert.False(leader.CanDisable);
        Assert.False(leader.CanEnable);
        Assert.False(leader.CanTransferLeadershipTo);

        Assert.True(byId[901].CanDisable);
        Assert.True(byId[901].CanTransferLeadershipTo);

        Assert.True(byId[902].CanEnable);
        Assert.False(byId[902].CanDisable);

        // PENDING activates only by confirming its email; LOCKED needs the security flow.
        Assert.False(byId[903].CanEnable);
        Assert.True(byId[903].CanResendEmailConfirmation);
        Assert.False(byId[904].CanEnable);
        Assert.False(byId[904].CanResendEmailConfirmation);

        // Editing is always offered, including for LOCKED — its email must stay correctable.
        Assert.True(byId[904].CanEdit);
    }

    [Fact]
    public async Task Department_and_campus_names_come_from_the_database()
    {
        var h = DepartmentLeaderTestHarness.Create();
        h.AddStaff();

        var result = await Run(h, new ListDepartmentPersonnelQuery());

        Assert.All(result.Items, item =>
        {
            Assert.Equal("Phòng ban 10", item.DepartmentName);
            Assert.Equal("Campus 1", item.CampusName);
        });
    }
}
