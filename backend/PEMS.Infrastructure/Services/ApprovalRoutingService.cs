using PEMS.Application.Common.Interfaces;
using PEMS.Domain.Constants;

namespace PEMS.Infrastructure.Services;

/// <summary>
/// Pure-logic service: maps visit scope to the first approval status.
/// </summary>
public sealed class ApprovalRoutingService : IApprovalRoutingService
{
    public string DetermineInitialStatus(string visitScope)
        => visitScope == VisitScopes.MultiCampus
            ? VisitRequestStatuses.PendingHoApproval
            : VisitRequestStatuses.PendingStaffLeadApproval;
}
