using System.Threading.Tasks;
using Xunit;

namespace PEMS.ApplicationTests.Delegations;

/// <summary>
/// Tests for the shared "submitted visit-request form snapshot" —
/// GetSubmittedVisitRequestFormDetailQueryHandler. Used by pre-approval review, the
/// approved/waiting-host detail and the rejected detail screens.
///
/// NOTE (environment): the PEMS.ApplicationTests folder has NO .csproj and is not part of
/// PEMS.slnx, and there is no EF Core InMemory provider referenced, so these cannot run under
/// `dotnet test` yet. They are documented as Skip stubs (the convention for every file in this
/// folder) so the required coverage is captured.
///
/// Visibility contract under test (canonical v10):
///   • Staff Leader → SINGLE_CAMPUS of own campus, ANY status (pending/approved/rejected/cancelled);
///                    MULTI_CAMPUS of own campus ONLY after HO approval; never MULTI pending/HO-rejected.
///   • HO           → MULTI_CAMPUS any status; never SINGLE_CAMPUS.
///   • Visitor      → only the request they own.
///   • Everyone else → 403. Out-of-scope direct URL → 403.
/// </summary>
public class GetSubmittedVisitRequestFormDetailQueryTests
{
    // 1. Staff Leader can view own-campus SINGLE_CAMPUS PENDING_APPROVAL.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task StaffLeader_View_OwnCampus_Single_Pending() => await Task.CompletedTask;

    // 2. Staff Leader can view own-campus SINGLE_CAMPUS REJECTED (incl. decision info populated).
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task StaffLeader_View_OwnCampus_Single_Rejected() => await Task.CompletedTask;

    // 3. Staff Leader can view own-campus SINGLE_CAMPUS APPROVED.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task StaffLeader_View_OwnCampus_Single_Approved() => await Task.CompletedTask;

    // 4. Staff Leader cannot view SINGLE_CAMPUS of another campus → 403.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task StaffLeader_Cannot_View_OtherCampus_Single() => await Task.CompletedTask;

    // 5. Staff Leader cannot view MULTI_CAMPUS PENDING_APPROVAL (before HO approval) → 403.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task StaffLeader_Cannot_View_Multi_Pending() => await Task.CompletedTask;

    // 6. Staff Leader cannot view MULTI_CAMPUS REJECTED by HO → 403.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task StaffLeader_Cannot_View_Multi_RejectedByHo() => await Task.CompletedTask;

    // 7. Staff Leader can view own-campus MULTI_CAMPUS APPROVED instance (after HO approve);
    //    only their own campus instance is surfaced in Campuses.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task StaffLeader_View_OwnCampus_Multi_Approved() => await Task.CompletedTask;

    // 8. Staff Leader cannot view MULTI_CAMPUS APPROVED that has no instance of their campus → 403.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task StaffLeader_Cannot_View_Multi_Approved_OtherCampusOnly() => await Task.CompletedTask;

    // 9. HO can view MULTI_CAMPUS PENDING_APPROVAL.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task Ho_View_Multi_Pending() => await Task.CompletedTask;

    // 10. HO can view MULTI_CAMPUS REJECTED.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task Ho_View_Multi_Rejected() => await Task.CompletedTask;

    // 11. HO cannot view SINGLE_CAMPUS → 403.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task Ho_Cannot_View_Single() => await Task.CompletedTask;

    // 12. Out-of-scope direct URL (e.g. Admin / regular Staff / Department / Student) → 403.
    [Fact(Skip = "ApplicationTests has no csproj/test runner in this environment")]
    public async Task OutOfScope_DirectUrl_Forbidden() => await Task.CompletedTask;
}
