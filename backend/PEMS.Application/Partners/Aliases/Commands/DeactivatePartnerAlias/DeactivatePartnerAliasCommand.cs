using MediatR;

namespace PEMS.Application.Partners.Aliases.Commands.DeactivatePartnerAlias;

/// <summary>DELETE /api/partners/{partnerId}/aliases/{aliasId} — soft delete (status → INACTIVE).</summary>
public sealed record DeactivatePartnerAliasCommand(ulong PartnerId, ulong AliasId)
    : IRequest<DeactivatePartnerAliasResponse>;

public sealed class DeactivatePartnerAliasResponse
{
    public ulong PartnerAliasId { get; set; }
    public string Status { get; set; } = "INACTIVE";
}
