using MediatR;

namespace PEMS.Application.Accounts.Queries.ViewAccountDetails;

/// <summary>
/// UC-98 View Account Details. Returns a safe (no auth secrets) detail projection for a
/// single account the caller is allowed to see. Scope is enforced in the handler:
/// HO may only view HO and Staff-Leader accounts; ADMIN may view any; a Staff Leader is
/// limited to their own campus.
/// </summary>
public sealed class ViewAccountDetailsQuery : IRequest<ViewAccountDetailsDto>
{
    /// <summary>Target account id.</summary>
    public ulong UserId { get; set; }
}
