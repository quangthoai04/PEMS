using MediatR;
using PEMS.Application.Partners.Common;

namespace PEMS.Application.Partners.Aliases.Commands.CreatePartnerAlias;

/// <summary>POST /api/partners/{partnerId}/aliases</summary>
public sealed class CreatePartnerAliasCommand : IRequest<PartnerAliasDto>
{
    public ulong PartnerId { get; set; }
    public string AliasName { get; set; } = string.Empty;
}
