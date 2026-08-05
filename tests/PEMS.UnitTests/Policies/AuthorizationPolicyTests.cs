using PEMS.Application.Common.Interfaces;
using PEMS.Application.Common.Security;
using PEMS.Domain.Entities.Campuses;
using PEMS.Domain.Entities.Delegations;
using Xunit;

namespace PEMS.UnitTests.Policies;

/// <summary>
/// Behaviour of the coarse authorization policies. Every case here corresponds to a real
/// defect: Campus granting ADMIN, an unmappable role/sub-role pair throwing instead of denying,
/// and CanViewVisitRequest ending in a blanket "return true" for four roles.
/// </summary>
public class AuthorizationPolicyTests
{
    private static FakeCurrentUser User(string roleCode, string subRole, ulong? campusId = null) =>
        new()
        {
            IsAuthenticated = true,
            UserId = 1UL,
            RoleCode = roleCode,
            SubRole = subRole,
            PrimaryCampusId = campusId,
        };

    // ── Campus: HO only (PERMISSION_MATRIX §5.14) ─────────────────────────────

    [Fact]
    public void CanAccessCampusManagement_allows_HO()
    {
        var policy = new RoleAccessPolicy();
        Assert.True(policy.CanAccessCampusManagement(User(RoleCode.Ho, SubRole.None)));
    }

    [Theory]
    [InlineData(RoleCode.Admin, SubRole.None)]
    [InlineData(RoleCode.Staff, SubRole.Leader)]
    [InlineData(RoleCode.Staff, SubRole.Staff)]
    [InlineData(RoleCode.Department, SubRole.Leader)]
    [InlineData(RoleCode.Department, SubRole.Staff)]
    [InlineData(RoleCode.Student, SubRole.None)]
    [InlineData(RoleCode.Visitor, SubRole.None)]
    public void CanAccessCampusManagement_denies_every_other_role(string roleCode, string subRole)
    {
        var policy = new RoleAccessPolicy();
        Assert.False(policy.CanAccessCampusManagement(User(roleCode, subRole)));
    }

    [Fact]
    public void CanManageCampus_denies_ADMIN()
    {
        // ADMIN is a technical administrator, not the campus business owner.
        var policy = new RoleAccessPolicy();
        Assert.False(policy.CanManageCampus(User(RoleCode.Admin, SubRole.None), (Campus?)null));
    }

    // ── Fail-closed on bad or missing identity ────────────────────────────────

    [Theory]
    [InlineData(RoleCode.Staff, SubRole.None)]
    [InlineData(RoleCode.Department, SubRole.None)]
    [InlineData("NOT_A_ROLE", SubRole.None)]
    public void An_unmappable_identity_denies_rather_than_throwing(string roleCode, string subRole)
    {
        var policy = new RoleAccessPolicy();
        var user = User(roleCode, subRole);

        // No exception may escape: an invalid pair is a 403, never a 500.
        Assert.False(policy.CanAccessCampusManagement(user));
        Assert.False(policy.CanAccessAccountManagement(user));
        Assert.False(policy.CanAccessDepartmentManagement(user));
        Assert.False(policy.CanAccessVisitManagement(user));
    }

    [Fact]
    public void An_anonymous_caller_is_denied_everywhere()
    {
        var policy = new RoleAccessPolicy();
        var anonymous = new FakeCurrentUser { IsAuthenticated = false };

        Assert.False(policy.CanAccessCampusManagement(anonymous));
        Assert.False(policy.CanAccessAccountManagement(anonymous));
        Assert.False(policy.CanAccessDepartmentManagement(anonymous));
        Assert.False(policy.CanAccessVisitManagement(anonymous));
    }

    [Fact]
    public void An_authenticated_caller_with_no_role_code_is_denied()
    {
        var policy = new RoleAccessPolicy();
        var noRole = new FakeCurrentUser { IsAuthenticated = true, UserId = 1UL, RoleCode = null };

        Assert.False(policy.CanAccessCampusManagement(noRole));
        Assert.False(policy.CanAccessVisitManagement(noRole));
    }

    // ── ADMIN is not a business superuser (§4.2, matrix §5.4) ─────────────────

    [Fact]
    public void ADMIN_cannot_reach_visit_management()
    {
        var policy = new RoleAccessPolicy();
        Assert.False(policy.CanAccessVisitManagement(User(RoleCode.Admin, SubRole.None)));
    }

    [Theory]
    [InlineData(RoleCode.Ho, SubRole.None)]
    [InlineData(RoleCode.Staff, SubRole.Leader)]
    [InlineData(RoleCode.Staff, SubRole.Staff)]
    [InlineData(RoleCode.Department, SubRole.Leader)]
    [InlineData(RoleCode.Department, SubRole.Staff)]
    [InlineData(RoleCode.Student, SubRole.None)]
    [InlineData(RoleCode.Visitor, SubRole.None)]
    public void Every_business_role_can_reach_visit_management(string roleCode, string subRole)
    {
        var policy = new RoleAccessPolicy();
        Assert.True(policy.CanAccessVisitManagement(User(roleCode, subRole)));
    }

    // ── CanViewVisitRequest no longer ends in a blanket allow ─────────────────

    [Theory]
    [InlineData(RoleCode.Staff, SubRole.Staff)]
    [InlineData(RoleCode.Department, SubRole.Leader)]
    [InlineData(RoleCode.Department, SubRole.Staff)]
    [InlineData(RoleCode.Student, SubRole.None)]
    public void CanViewVisitRequest_does_not_blanket_allow_assignment_scoped_roles(string roleCode, string subRole)
    {
        // These four used to hit `return true` and see every request in the system.
        // Their real visibility comes from the delegation read model, not from here.
        var policy = new RoleAccessPolicy();
        var request = new VisitRequest { VisitRequestId = 10UL, VisitorUserId = 999UL, VisitScope = "SINGLE_CAMPUS" };

        Assert.False(policy.CanViewVisitRequest(User(roleCode, subRole, campusId: 1UL), request));
    }

    [Fact]
    public void CanViewVisitRequest_lets_a_Visitor_see_only_their_own_request()
    {
        var policy = new RoleAccessPolicy();
        var own = new VisitRequest { VisitRequestId = 1UL, VisitorUserId = 1UL, VisitScope = "SINGLE_CAMPUS" };
        var other = new VisitRequest { VisitRequestId = 2UL, VisitorUserId = 2UL, VisitScope = "SINGLE_CAMPUS" };

        Assert.True(policy.CanViewVisitRequest(User(RoleCode.Visitor, SubRole.None), own));
        Assert.False(policy.CanViewVisitRequest(User(RoleCode.Visitor, SubRole.None), other));
    }

    [Fact]
    public void CanProcessVisitRequest_keeps_a_Staff_Leader_inside_their_own_campus()
    {
        var policy = new RoleAccessPolicy();
        var request = new VisitRequest { VisitRequestId = 1UL, VisitScope = "SINGLE_CAMPUS" };
        request.CampusInstances.Add(new VisitRequestCampus { CampusId = 7UL });

        Assert.True(policy.CanProcessVisitRequest(User(RoleCode.Staff, SubRole.Leader, campusId: 7UL), request));
        // Cross-campus: same scope, different campus — must be refused (anti-IDOR).
        Assert.False(policy.CanProcessVisitRequest(User(RoleCode.Staff, SubRole.Leader, campusId: 8UL), request));
    }

    [Fact]
    public void CanProcessVisitRequest_keeps_HO_on_multi_campus_only()
    {
        var policy = new RoleAccessPolicy();
        var multi = new VisitRequest { VisitRequestId = 1UL, VisitScope = "MULTI_CAMPUS" };
        var single = new VisitRequest { VisitRequestId = 2UL, VisitScope = "SINGLE_CAMPUS" };

        Assert.True(policy.CanProcessVisitRequest(User(RoleCode.Ho, SubRole.None), multi));
        Assert.False(policy.CanProcessVisitRequest(User(RoleCode.Ho, SubRole.None), single));
    }

    // ── EffectiveRole: eight distinct values, no implicit grants ──────────────

    [Fact]
    public void EffectiveRole_maps_all_eight_combinations_distinctly()
    {
        var resolved = new[]
        {
            EffectiveRole.Resolve(RoleCode.Admin, SubRole.None),
            EffectiveRole.Resolve(RoleCode.Ho, SubRole.None),
            EffectiveRole.Resolve(RoleCode.Staff, SubRole.Leader),
            EffectiveRole.Resolve(RoleCode.Staff, SubRole.Staff),
            EffectiveRole.Resolve(RoleCode.Department, SubRole.Leader),
            EffectiveRole.Resolve(RoleCode.Department, SubRole.Staff),
            EffectiveRole.Resolve(RoleCode.Student, SubRole.None),
            EffectiveRole.Resolve(RoleCode.Visitor, SubRole.None),
        };

        Assert.Equal(8, resolved.Distinct().Count());
        Assert.NotEqual(resolved[2], resolved[3]); // Staff Leader != Staff
        Assert.NotEqual(resolved[4], resolved[5]); // Department Lead != Department
    }

    [Theory]
    [InlineData(RoleCode.Staff, null)]
    [InlineData(RoleCode.Staff, SubRole.None)]
    [InlineData(RoleCode.Department, null)]
    [InlineData(RoleCode.Department, "SUPERVISOR")]
    [InlineData("SUPERUSER", SubRole.None)]
    public void EffectiveRole_refuses_to_guess(string roleCode, string? subRole)
    {
        Assert.Throws<InvalidOperationException>(() => EffectiveRole.Resolve(roleCode, subRole));
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated { get; set; }
        public ulong? UserId { get; set; }
        public string? Email { get; set; }
        public ulong? RoleId { get; set; }
        public string? RoleCode { get; set; }
        public string? SubRole { get; set; }
        public ulong? PrimaryCampusId { get; set; }
        public ulong? DepartmentId { get; set; }
        public ulong? SessionId { get; set; }
        public string? LoginPortal { get; set; }
    }
}
