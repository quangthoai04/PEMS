namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Determines the initial approval status and target management page for a newly
/// submitted visit request, based on scope and request type.
/// </summary>
public interface IApprovalRoutingService
{
    /// <summary>
    /// Returns the status string to assign to a newly verified visit request.
    /// <list type="bullet">
    ///   <item>MULTI_CAMPUS → PENDING_HO_APPROVAL (displayed on HO Management Page)</item>
    ///   <item>SINGLE_CAMPUS → PENDING_STAFF_LEAD_APPROVAL (displayed on StaffLead Management Page)</item>
    /// </list>
    /// </summary>
    string DetermineInitialStatus(string visitScope);
}
