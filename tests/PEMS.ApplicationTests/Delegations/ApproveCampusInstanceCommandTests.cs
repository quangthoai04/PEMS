using System.Threading.Tasks;
using Xunit;

namespace PEMS.ApplicationTests.Delegations;

public class ApproveCampusInstanceCommandTests
{
    [Fact(Skip = "Pending UC specification")]
    public async Task Handle_Should_Approve_CampusInstance_And_Assign_Host()
    {
        // TODO: Staff Leader duyệt campus instance & approve bắt buộc host
    }

    [Fact(Skip = "Pending UC specification")]
    public async Task Handle_Should_Allow_SelfHost()
    {
        // TODO: Staff Leader có thể chọn self-host
    }

    [Fact(Skip = "Pending UC specification")]
    public async Task Handle_Should_Return_Warning_On_HostConflict_Without_Blocking()
    {
        // TODO: host conflict chỉ warning, không block
    }

    [Fact(Skip = "Pending UC specification")]
    public async Task Handle_Should_Aggregate_Status_To_PartiallyApproved_Or_Approved()
    {
        // TODO: aggregate PARTIALLY_APPROVED/APPROVED/REJECTED/CANCELLED
    }
}
