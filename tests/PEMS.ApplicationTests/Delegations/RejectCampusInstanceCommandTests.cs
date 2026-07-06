using System.Threading.Tasks;
using Xunit;

namespace PEMS.ApplicationTests.Delegations;

public class RejectCampusInstanceCommandTests
{
    [Fact(Skip = "Pending UC specification")]
    public async Task Handle_Should_Reject_Specific_Campus_Instance()
    {
        // TODO: reject từng campus
    }

    [Fact(Skip = "Pending UC specification")]
    public async Task Handle_Should_Aggregate_Status_Correctly_After_Reject()
    {
        // TODO: aggregate PARTIALLY_APPROVED/APPROVED/REJECTED/CANCELLED sau khi reject
    }
}
