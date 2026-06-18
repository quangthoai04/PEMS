using PEMS.Application.Common.DTOs;
using PEMS.Domain.Entities.Delegations;

namespace PEMS.Application.Common.Interfaces;

/// <summary>
/// Encapsulates the creation of a <see cref="VisitRequest"/> and all its child
/// aggregates (campus instances, guest members). Does NOT commit — the caller
/// is responsible for calling SaveChangesAsync after applying the status.
/// </summary>
public interface IVisitRequestService
{
    /// <summary>
    /// Builds and adds to the DbContext a new <see cref="VisitRequest"/> together
    /// with its campus instances and guest-member rows. Does not call SaveChanges.
    /// </summary>
    Task<VisitRequest> CreateAsync(
        PendingVisitRequestFormData formData,
        string visitorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
